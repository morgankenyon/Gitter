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
open Microsoft.AspNetCore.Authentication.Cookies
open System.Security.Claims
open Microsoft.AspNetCore.Authentication
open Constants



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

    let viewGitsHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! gits = selectGits()

                return! ctx.WriteHtmlViewAsync (Views.gitFeed gits)
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

    let loginHandler : HttpHandler =
        Views.loginView()
        |> htmlView

    let loginRequestHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! loginRequest = ctx.BindFormAsync<LoginRequest>()

                let! signInInfo = searchForUser loginRequest.Email

                match signInInfo with
                | Some sir ->
                    let saltArray =
                        sir.Salt
                        |> Convert.FromHexString

                    let rehashedPassword = defaultHashing loginRequest.Password saltArray

                    if rehashedPassword = sir.HashedPassword then
                        let mutable claims: Claim list = []
                        claims <- new Claim(ClaimTypes.Name, loginRequest.Email) :: claims
                        claims <- new Claim(ClaimTypes.Role, AdminRole) :: claims

                        let claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)

                        let authProperties = new AuthenticationProperties()

                        do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties)

                        return! ctx.WriteStringAsync ("Login worked")
                    else
                        return! ctx.WriteStringAsync ("Login failed again")
                    //return! (redirectTo false "/"  next ctx)
                | None ->
                    return! ctx.WriteStringAsync ("Login failed")

            }


module Api =
    open Handlers
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true

    let webApp =
        choose [
            GET >=>
                choose [
                    route "/git" >=> viewGitsHandler
                    route "/git/new" >=> addGitHandler
                    route "/login" >=> loginHandler
                    route "/signup" >=> signUpHandler
                ]
            POST >=>
                choose [
                    route "/git/new" >=> submitGitHandler
                    route "/login" >=> loginRequestHandler
                    route "/signup" >=> signedUpHandler
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

    let getCookiePolicyOptions () : CookiePolicyOptions =
        let cookiePolicyOptions = new CookiePolicyOptions()
        cookiePolicyOptions.MinimumSameSitePolicy <- SameSiteMode.Strict
        cookiePolicyOptions

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
            .UseCookiePolicy(getCookiePolicyOptions())
            .UseGiraffe(webApp)

    //Cookie policy used from: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-6.0
    //Does not work in server farm or load balancing
    let cookieOptions(opts: CookieAuthenticationOptions) =
        opts.ExpireTimeSpan <- TimeSpan.FromMinutes(20)
        opts.SlidingExpiration <- true
        opts.AccessDeniedPath <- "/Forbidden"

    let configureServices (services : IServiceCollection) =
        services.AddCors()    |> ignore
        services.AddGiraffe() |> ignore
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookieOptions) |> ignore

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