module Gitter.Data

open Dapper
open Models
open Npgsql
open Options
open System.Data
open System.Threading

let private makeCommand (sql: string) (ct: CancellationToken) =
    new CommandDefinition(sql, ?cancellationToken = Some(ct))

let private makeParamCommand (sql: string) (dbParams: obj) (ct: CancellationToken) =
    new CommandDefinition(sql, dbParams, ?cancellationToken = Some(ct))

let private makeConnStr (dbOptions: DatabaseOptions) =
    new NpgsqlConnection(dbOptions.ConnectionString) :> IDbConnection

let private noParamSelect<'T> (dbInfo: DbInfo) (sql: string) =
    task {
        try
            use conn = makeConnStr dbInfo.DatabaseOptions
            conn.Open()

            let! result =
                makeCommand sql dbInfo.Token
                |> conn.QueryAsync<'T>

            return Ok result
        with
        | ex -> return Error ex
    }

let private paramSelect<'T> (dbInfo: DbInfo) (sql: string) (dbParams: obj) =
    task {
        try
            use conn = makeConnStr dbInfo.DatabaseOptions
            conn.Open()

            let! result =
                makeParamCommand sql dbParams dbInfo.Token
                |> conn.QueryAsync<'T>

            return Ok result
        with
        | ex -> return Error ex
    }

let private insertSql (dbInfo: DbInfo) (sql: string) (dbParams: obj) =
    task {
        try

            use conn = makeConnStr dbInfo.DatabaseOptions
            conn.Open()

            let command = makeParamCommand sql dbParams dbInfo.Token
            let! insertedId = conn.ExecuteScalarAsync<int>(command)

            return Ok insertedId
        with
        | ex -> return Error ex
    }

let selectGits (dbInfo: DbInfo) =
    let sql =
        """
        SELECT
            *
	    FROM dbo.gits
        ORDER BY created_at DESC
        """

    noParamSelect<Git> dbInfo sql

let insertUser (newUser: HashedNewUser) (salt: string) (dbInfo: DbInfo) =
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

    let dbParams =
        {| firstName = newUser.FirstName
           lastName = newUser.LastName
           email = newUser.Email
           hashed_password = newUser.HashedPassword
           salt = salt |}

    insertSql dbInfo sql dbParams


let insertGit (newGit: NewGit) (userId: int) (dbInfo: DbInfo) =
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

    let dbParams =
        {| gitText = newGit.GitText
           userId = userId |}

    insertSql dbInfo sql dbParams

let searchForUser (email: string) (dbInfo: DbInfo) =
    let sql =
        """
        SELECT
            hashed_password,
            salt
        FROM dbo.Users
        WHERE email = @email
        """

    let dbParams = {| email = email |}

    paramSelect<SignInInfo> dbInfo sql dbParams
