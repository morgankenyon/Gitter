module Gitter.Extensions

open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Options
open Models
open Options
open System.Runtime.CompilerServices

[<Extension>]
type HttpContextExts =
    [<Extension>]
    static member inline BuildDbInfo(hc: HttpContext) =
        let dbOptions = hc.GetService<IOptions<DatabaseOptions>>()
        let cToken = hc.RequestAborted

        { DatabaseOptions = dbOptions.Value
          Token = cToken }
