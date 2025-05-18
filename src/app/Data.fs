module Gitter.Data

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

let searchForUser (email: string) =
    let sql =
        """
        SELECT
            hashed_password,
            salt
        FROM dbo.Users
        WHERE email = @email
        """

    task {
        use conn = new NpgsqlConnection(connStr)
        let dbParams =
            {|
                email = email
            |}
        conn.Open()

        let! signInInfos = conn.QueryAsync<SignInInfo>(sql, dbParams) //TODO - cancellationToken

        return
            if Seq.isEmpty signInInfos then
                None
            else
                signInInfos
                |> Seq.head
                |> Some
    }