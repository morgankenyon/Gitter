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
    type NewGit =
        {
            GitText: string
        }

    [<CLIMutable>]
    type Git =
        {
            GitId : int
            CreatedAt : DateTime
            GitText : string
            UserId : int
        }
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

    let insertGit (newGit: NewGit) (userId: int) =
        let sql =
            """
            INSERT INTO dbo.gits (
                git_text,
                user_id
            ) VALUES (
                @gitText,
                @userId
            ) RETURNING git_id;
            """

        task {
            use conn = new NpgsqlConnection(connStr)
            let dbParams =
                {|
                    gitText = newGit.GitText
                    userId = userId
                |}
            conn.Open()

            let! gitId = conn.ExecuteScalarAsync<int>(sql, dbParams) //TODO - cancellationToken

            return gitId
        }

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [ _lang "en" ] [
            head [] [
                meta [ _charset "utf-8" ]
                meta [ _name "viewport"]
                meta [ _name "color-scheme" 
                       _content "light" ]
                title []  [ encodedText "Gitter" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/pico.min.css" ]
            ]
            body [] [
                main [ _class "container" ] content
            ]
        ]

    let partial () =
        h1 [] [ encodedText "Gitter" ]

    let addGitView () =
        [
            partial()
            form [ _method "post" ] [
                input [ _type "text"
                        _name "gitText"
                        _placeholder "Git Text"
                        _required ]
                input [ _type "submit"
                        _value "Submit" ]                
            ]
        ] |> layout

    let signUpView () =
        [
            partial()
            //p [] [ encodedText "Hello there" ]
            form [ _method "post"
                   _action "signup" ] [
                input [ _type "text"
                        _name "firstName"
                        _placeholder "First Name"
                        _autocomplete "given-name"
                        _required ]
                input [ _type "text"
                        _name "lastName"
                        _placeholder "Last Name"
                        _autocomplete "family-name"
                        _required ]
                input [ _type "text"
                        _name "email"
                        _placeholder "Email"
                        _autocomplete "email"
                        _required ]
                input [ _type "password"
                        _name "password"
                        _placeholder "Password"
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

    let addGitHandler : HttpHandler =
        Views.addGitView()
        |> htmlView

    let submitGitHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! newGit = ctx.BindFormAsync<NewGit>()

                let! gitId = insertGit newGit 2

                return! ctx.WriteStringAsync (sprintf "GitId: %d" gitId)
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
                    route "/git/new" >=> addGitHandler
                ]
            POST >=>
                choose [
                    route "/signup" >=> signedUpHandler
                    route "/git/new" >=> submitGitHandler
                    //route "/user" >=> insertUserHandler
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