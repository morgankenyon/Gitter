module Gitter.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http

module Models =
    type NewUser =
        {
            FirstName : string
            LastName : string
            Phone: string
        }

    [<CLIMutable>]
    type User =
        {
            UserId : int32
            FirstName : string
            LastName : string
            Phone: string
        }

module Data =
    open Dapper
    open Models
    open Npgsql
    open System.Data

    let connStr = "Host=localhost;Username=postgres;Password=password123;Database=gitter"
    let getAllUsers () =
        //let sql = "SELECT user_id as userId, first_name as firstName, last_name as lastName, phone FROM dbo.users"
        let sql = "SELECT * FROM dbo.users"

        task {
            use conn = new NpgsqlConnection(connStr) :> IDbConnection
            conn.Open()

            let! dbUsers = conn.QueryAsync<User>(sql) //TODO - cancellationToken

            return dbUsers
        }

    let insertUser (newUser: NewUser) =
        let sql = 
            """
            INSERT INTO dbo.users (
                first_name,
                last_name,
                phone
            ) VALUES (
                @firstName,
                @lastName,
                @phone
            ) RETURNING user_id;
            """

        task {
            use conn = new NpgsqlConnection(connStr)
            let dbParams = {| firstName = newUser.FirstName; lastName = newUser.LastName; phone = newUser.Phone |}
            conn.Open()

            let! userId = conn.ExecuteScalarAsync<int>(sql, dbParams) //TODO - cancellationToken

            return userId
        }

module Handlers =
    open Data
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
                let! newUser = ctx.BindJsonAsync<NewUser>()
                let! userId = insertUser newUser

                return! ctx.WriteStringAsync (sprintf "User:d: %d" userId)
            }


module Api =
    open Data
    open DbUp
    open Handlers
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true

    let webApp =
        choose [
            GET >=>
                choose [
                    route "/user" >=> getAllUsersHandler
                ]
            POST >=>
                choose [
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
        //let webRoot     = Path.Combine(contentRoot, "WebRoot")
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .UseContentRoot(contentRoot)
                        //.UseWebRoot(webRoot)
                        .Configure(Action<IApplicationBuilder> configureApp)
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        |> ignore)
            .Build()
            .Run()
        0