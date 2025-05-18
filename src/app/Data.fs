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

//let getAllUsers (dbOptions: DatabaseOptions) (ct: CancellationToken) =
//    let sql = "SELECT * FROM dbo.users"

//    task {
//        use conn = makeConnStr dbOptions
//        conn.Open()

//        let command = makeCommand sql ct
//        let! dbUsers = conn.QueryAsync<User>(command)

//        return dbUsers
//    }

let insertUser (dbOptions: DatabaseOptions) (newUser: HashedNewUser) (salt: string) (ct: CancellationToken) =
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
        use conn = makeConnStr dbOptions
        let dbParams =
            {|
                firstName = newUser.FirstName
                lastName = newUser.LastName
                email = newUser.Email
                hashed_password = newUser.HashedPassword
                salt = salt
            |}
        conn.Open()

        let cmd = makeParamCommand sql dbParams ct
        let! userId = conn.ExecuteScalarAsync<int>(cmd)

        return userId
    }

let insertGit (dbOptions: DatabaseOptions) (newGit: NewGit) (userId: int) (ct: CancellationToken) =
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
        use conn = makeConnStr dbOptions
        let dbParams =
            {|
                gitText = newGit.GitText
                userId = userId
            |}
        conn.Open()

        let command = makeParamCommand sql dbParams ct
        let! gitId = conn.ExecuteScalarAsync<int>(command)

        return gitId
    }

let selectGits (dbOptions: DatabaseOptions) (ct: CancellationToken) =
    let sql =
        """
        SELECT
            *
	    FROM dbo.gits
        ORDER BY created_at DESC
        """
    task {
        use conn = makeConnStr dbOptions
        conn.Open()

        let command = makeCommand sql ct
        let! gits = conn.QueryAsync<Git>(command)

        return gits
    }

let searchForUser (dbOptions: DatabaseOptions) (email: string) (ct: CancellationToken) =
    let sql =
        """
        SELECT
            hashed_password,
            salt
        FROM dbo.Users
        WHERE email = @email
        """

    task {
        use conn = makeConnStr dbOptions
        let dbParams =
            {|
                email = email
            |}
        conn.Open()

        let command = makeParamCommand sql dbParams ct
        let! signInInfos = conn.QueryAsync<SignInInfo>(command)

        return
            if Seq.isEmpty signInInfos then
                None
            else
                signInInfos
                |> Seq.head
                |> Some
    }