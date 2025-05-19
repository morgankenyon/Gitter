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
open Microsoft.Extensions.Configuration
open Options
open Microsoft.Extensions.Options




module Handlers =
    open Data
    open Extensions
    open Logic
    open Models

    let logErrorHandler (ctx: HttpContext) (loggerName: string) (exn: exn) =
        task {
            let logger = ctx.GetLogger("submitGitHandler")
            logger.LogError(exn, "Error occurred when interacting with database")
            return! ctx.WriteStringAsync "Error Occurred"
        }

    let addGitHandler : HttpHandler =
        Views.addGitView()
        |> htmlView

    let submitGitHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! newGit = ctx.BindFormAsync<NewGit>()

                let! gitIdResult = 
                    ctx.BuildDbInfo()
                    |> insertGit newGit 2

                match gitIdResult with
                | Ok gitId ->
                    return! ctx.WriteStringAsync (sprintf "GitId: %d" gitId)
                | Error exn ->
                    return! logErrorHandler ctx "submitGitHandler" exn
            }

    let viewGitsHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let! gitResult = 
                    ctx.BuildDbInfo()
                    |> selectGits

                match gitResult with
                | Ok gits ->
                    return! ctx.WriteHtmlViewAsync (Views.gitFeed gits)
                | Error exn ->
                    return! logErrorHandler ctx "viewGitsHandler" exn
            }

    let signUpHandler : HttpHandler =
        Views.signUpView()
        |> htmlView

    let signedUpHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                let dbOptions = ctx.GetService<IOptions<DatabaseOptions>>()
                let! newUser = ctx.BindFormAsync<UnhashedNewUser>()
                let salt = generateDbSalt 64 //extract to some config
                let hashedUser = hashUserRequest newUser salt
                let hexSalt = Convert.ToHexString(salt)
                let! userIdResult = 
                    ctx.BuildDbInfo()
                    |> insertUser hashedUser hexSalt

                match userIdResult with
                | Ok userId ->
                    return! ctx.WriteStringAsync (sprintf "UserId: %d" userId)
                | Error exn ->
                    return! logErrorHandler ctx "signedUpHandler" exn
            }

    let loginHandler : HttpHandler =
        Views.loginView()
        |> htmlView

    let loginRequestHandler : HttpHandler =
        fun (next : HttpFunc) (ctx: HttpContext) ->
            task {
                let! loginRequest = ctx.BindFormAsync<LoginRequest>()
                let! signInResult = 
                    ctx.BuildDbInfo()
                    |> searchForUser loginRequest.Email
                
                match signInResult with
                | Ok signInInfos when not (Seq.isEmpty(signInInfos)) ->
                    let signInInfo = Seq.head signInInfos
                    let saltArray =
                        signInInfo.Salt
                        |> Convert.FromHexString

                    let rehashedPassword = defaultHashing loginRequest.Password saltArray

                    if rehashedPassword = signInInfo.HashedPassword then
                        let mutable claims: Claim list = []
                        claims <- new Claim(ClaimTypes.Name, loginRequest.Email) :: claims
                        claims <- new Claim(ClaimTypes.Role, AdminRole) :: claims //replace with actual roles

                        let claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)

                        let authProperties = new AuthenticationProperties()
                        do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties)

                        //return! ctx.WriteStringAsync ("Login worked")
                        return! (redirectTo false "/git"  next ctx)
                    else
                        return! ctx.WriteStringAsync ("Login failed again")
                | Ok _ ->
                    return! ctx.WriteStringAsync ("Could not sign in")
                | Error exn ->
                    return! logErrorHandler ctx "loginRequestHandler" exn
            }

    let logoutHandler : HttpHandler =
        fun (_ : HttpFunc) (ctx: HttpContext) ->
            task {
                do! ctx.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme)
                return! ctx.WriteStringAsync ("Logged Out")
            }

    let notAdmin = 
        RequestErrors.FORBIDDEN
            "Permission denied. You must be an admin."
    let mustBeLoggedIn = requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)

    
    
    let mustBeAdmin = requiresRole Constants.AdminRole notAdmin

    let secretHandler : HttpHandler =
        Views.secretView "This is a normal secret"
        |> htmlView

    let adminSecretHandler : HttpHandler =
        Views.secretView "This is an Admin secret"
        |> htmlView


module Api =
    open Handlers
    open Options
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores <- true

    let webApp =
        choose [
            GET >=>
                choose [
                    route "/git" >=> viewGitsHandler
                    route "/git/new" >=> addGitHandler
                    route "/login" >=> loginHandler
                    route "/logout" >=> logoutHandler
                    route "/signup" >=> signUpHandler
                    route "/secret" >=> mustBeLoggedIn >=> secretHandler
                    route "/adminsecret" >=> mustBeLoggedIn >=> mustBeAdmin >=> adminSecretHandler
                ]
            POST >=>
                choose [
                    route "/git/new" >=> submitGitHandler
                    route "/login" >=> loginRequestHandler
                    route "/signup" >=> signedUpHandler
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

    let authenticationOptions (o: AuthenticationOptions) =
        o.DefaultAuthenticateScheme <- CookieAuthenticationDefaults.AuthenticationScheme
        o.DefaultChallengeScheme <- CookieAuthenticationDefaults.AuthenticationScheme

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
            .UseAuthentication()
            .UseGiraffe(webApp)

    //Cookie policy used from: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-6.0
    //Does not work in server farm or load balancing
    let cookieOptions(opts: CookieAuthenticationOptions) =
        opts.ExpireTimeSpan <- TimeSpan.FromMinutes(20)
        opts.SlidingExpiration <- true
        opts.AccessDeniedPath <- "/Forbidden"
        opts.LoginPath <- "/login"
        opts.Cookie.Name <- "GitterCookie"
        opts.Cookie.HttpOnly <- true
        opts.Cookie.SameSite <- SameSiteMode.Strict

    let configureServices (services : IServiceCollection) =
        services.AddCors()    |> ignore
        services.AddGiraffe() |> ignore
        services.AddAuthentication(authenticationOptions)
            .AddCookie(cookieOptions) |> ignore
        

    let configureLogging (builder : ILoggingBuilder) =
        builder.AddConsole()
               .AddDebug() |> ignore

    let configureConfigOptions (context: WebHostBuilderContext) (services : IServiceCollection) =
        services.Configure<DatabaseOptions>(
            context.Configuration.GetSection(DatabaseOptions.Database)) |> ignore

    let configureAppConfiguration (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
        
        config
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, true)
            .AddEnvironmentVariables() |> ignore

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
                        .ConfigureAppConfiguration(configureAppConfiguration)
                        .Configure(Action<IApplicationBuilder> configureApp)
                        .ConfigureServices(configureServices)
                        .ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureConfigOptions)
                        .ConfigureLogging(configureLogging)
                        |> ignore)
            .Build()
            .Run()
        0