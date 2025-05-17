module Gitter.App

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open System.Security.Cryptography
open System.Text

module Models =
    [<CLIMutable>]
    type UnhashedNewUser =
        {
            FirstName : string
            LastName : string
            Email: string
            Password: string
        }

    type HashedNewUser =
        {
            FirstName : string
            LastName : string
            Email: string
            HashedPassword: string
        }

    [<CLIMutable>]
    type User =
        {
            UserId : int32
            FirstName : string
            LastName : string
            Email: string
        }

module Logic =
    open Models
    let genericHashing (iterations: int) (keySize: int) (hashAlgorithm: HashAlgorithmName) (password: string) (salt: byte array)  =
        let hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, hashAlgorithm, keySize)
        Convert.ToHexString hash
    let defaultHashing =
        genericHashing 350000 64 HashAlgorithmName.SHA512

    let hashUserRequest (newUser: UnhashedNewUser) (salt: byte array) : HashedNewUser =
        let hashedPassword = defaultHashing newUser.Password salt
        {
            FirstName = newUser.FirstName
            LastName = newUser.LastName
            Email = newUser.Email
            HashedPassword = hashedPassword
        }

    let generateDbSalt (keySize : int) =
        RandomNumberGenerator.GetBytes(keySize)

module Data =
    open Dapper
    open Models
    open Npgsql
    open System.Data

    let connStr = "Host=localhost;Username=postgres;Password=password123;Database=gitter"
    let getAllUsers () =
        let sql = "SELECT * FROM dbo.users"

        task {
            use conn = new NpgsqlConnection(connStr) :> IDbConnection
            conn.Open()

            let! dbUsers = conn.QueryAsync<User>(sql) //TODO - cancellationToken

            return dbUsers
        }

    let insertUser (newUser: HashedNewUser) (salt: string) =
        let sql = 
            """
            INSERT INTO dbo.users (
                first_name,
                last_name,
                email,
                hashed_password,
                salt
            ) VALUES (
                @firstName,
                @lastName,
                @email,
                @hashed_password,
                @salt
            ) RETURNING user_id;
            """

        task {
            use conn = new NpgsqlConnection(connStr)
            let dbParams =
                {|
                    firstName = newUser.FirstName
                    lastName = newUser.LastName
                    email = newUser.Email
                    hashed_password = newUser.HashedPassword
                    salt = salt
                |}
            conn.Open()

            let! userId = conn.ExecuteScalarAsync<int>(sql, dbParams) //TODO - cancellationToken

            return userId
        }

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "Gitter" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "Gitter" ]

    let signUpView () =
        [
            partial()
            p [] [ encodedText "Hello there" ]
            form [ _method "post"
                   _action "signup" ] [
                input [ _type "text"
                        _name "firstName"
                        _required ]
                input [ _type "text"
                        _name "lastName"
                        _required ]
                input [ _type "text"
                        _name "email"
                        _required ]
                input [ _type "password"
                        _name "password"
                        _required ]
                input [ _type "submit"
                        _value "Submit" ]
            ]
        ] |> layout

    let signedUpView () =
        [
            partial()
            p [] [ encodedText "Check your email for confirmation" ]
        ] |> layout

module Handlers =
    open Data
    open Logic
    open Models
    let getAllUsersHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! users = getAllUsers()

                return! ctx.WriteJsonAsync users
            }

    let insertUserHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                //bind json
                let! newUser = ctx.BindJsonAsync<UnhashedNewUser>()
                let salt = generateDbSalt 64 //extract to some config
                let hashedUser = hashUserRequest newUser salt
                let! userId = insertUser hashedUser ""

                return! ctx.WriteStringAsync (sprintf "UserId: %d" userId)
            }

    let signUpHandler : HttpHandler =
        Views.signUpView()
        |> htmlView

    let signedUpHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                //bind form
                let! newUser = ctx.BindFormAsync<UnhashedNewUser>()
                let salt = generateDbSalt 64 //extract to some config
                let hashedUser = hashUserRequest newUser salt
                let hexSalt = Convert.ToHexString(salt)
                let! userId = insertUser hashedUser hexSalt

                return! ctx.WriteStringAsync (sprintf "UserId: %d" userId)
            }


module Api =
    open Handlers
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true

    let webApp =
        choose [
            GET >=>
                choose [
                    route "/signup" >=> signUpHandler
                    route "/user" >=> getAllUsersHandler
                ]
            POST >=>
                choose [
                    route "/signup" >=> signedUpHandler
                    route "/user" >=> insertUserHandler
                ]
            setStatusCode 404 >=> text "Not Found" ]

    // ---------------------------------
    // Error handler
    // ---------------------------------

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    // ---------------------------------
    // Config and Main
    // ---------------------------------

    let configureCors (builder : CorsPolicyBuilder) =
        builder
            .WithOrigins(
                "http://localhost:5000",
                "https://localhost:5001")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp)

    let configureServices (services : IServiceCollection) =
        services.AddCors()    |> ignore
        services.AddGiraffe() |> ignore

    let configureLogging (builder : ILoggingBuilder) =
        builder.AddConsole()
               .AddDebug() |> ignore

    [<EntryPoint>]
    let main args =

        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot     = Path.Combine(contentRoot, "WebRoot")
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .UseContentRoot(contentRoot)
                        .UseWebRoot(webRoot)
                        .Configure(Action<IApplicationBuilder> configureApp)
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        |> ignore)
            .Build()
            .Run()
        0