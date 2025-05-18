module Gitter.Logic

open Models
open System
open System.Security.Cryptography
open System.Text

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