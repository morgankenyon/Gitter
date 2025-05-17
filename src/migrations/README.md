# Gitter.Migrations

This project leverages [DbUp](https://github.com/DbUp/DbUp) to manage Gitter's postgres db migrations.

## Setup

1. Ensure a postgres database named `gitter` already exists. (keep the name all lowercase)
1. Confirm connection string in [Program.fs](./Program.fs) is pointing to your postgres instance.
1. Run `dotnet run` in current directory to apply any outstanding migrations

## Usage

1. Create a new DB script (keep the sequential naming intact)
1. Ensure this new file is an "Embedded Resource" and its set to preserve latest.
1. Run `dotnet run` in the migrations directory to apply this to your postgres instance.