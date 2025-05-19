module Gitter.Models

open System
open System.Threading
open Options

[<CLIMutable>]
type NewGit = { GitText: string }

[<CLIMutable>]
type Git =
    { GitId: int
      CreatedAt: DateTime
      GitText: string
      UserId: int }

[<CLIMutable>]
type UnhashedNewUser =
    { FirstName: string
      LastName: string
      Email: string
      Password: string }

type HashedNewUser =
    { FirstName: string
      LastName: string
      Email: string
      HashedPassword: string }

[<CLIMutable>]
type User =
    { UserId: int32
      FirstName: string
      LastName: string
      Email: string }

[<CLIMutable>]
type SignInInfo =
    { HashedPassword: string
      Salt: string }

[<CLIMutable>]
type LoginRequest = { Email: string; Password: string }

type DbInfo =
    { DatabaseOptions: DatabaseOptions
      Token: CancellationToken }
