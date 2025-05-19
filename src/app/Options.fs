module Gitter.Options

type DatabaseOptions() =
    let mutable connStr = ""

    member this.ConnectionString
        with get () = connStr
        and set (value) = connStr <- value

    static member Database = "Database"
