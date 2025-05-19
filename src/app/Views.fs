module Gitter.Views

open Giraffe.ViewEngine
open Models

let layout (content: XmlNode list) =
    html [ _lang "en" ] [
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport" ]
            meta [ _name "color-scheme"
                   _content "light" ]
            title [] [ encodedText "Gitter" ]
            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/pico.min.css" ]
        ]
        body [] [
            main [ _class "container" ] content
        ]
    ]

let partial () = h1 [] [ encodedText "Gitter" ]

let addGitView () =
    [ partial ()
      form [ _method "post" ] [
          input [ _type "text"
                  _name "gitText"
                  _placeholder "Git Text"
                  _required ]
          input [ _type "submit"
                  _value "Submit" ]
      ] ]
    |> layout

let signUpView () =
    [ partial ()
      //p [] [ encodedText "Hello there" ]
      form [ _method "post"; _action "signup" ] [
          input [ _type "text"
                  _name "firstName"
                  _placeholder "First Name"
                  _autocomplete "given-name"
                  _required ]
          input [ _type "text"
                  _name "lastName"
                  _placeholder "Last Name"
                  _autocomplete "family-name"
                  _required ]
          input [ _type "text"
                  _name "email"
                  _placeholder "Email"
                  _autocomplete "email"
                  _required ]
          input [ _type "password"
                  _name "password"
                  _placeholder "Password"
                  _required ]
          input [ _type "submit"
                  _value "Submit" ]
      ] ]
    |> layout

let signedUpView () =
    [ partial ()
      p [] [
          encodedText "Check your email for confirmation"
      ] ]
    |> layout

let loginView () =
    [ partial ()
      form [ _method "post" ] [
          input [ _type "text"
                  _name "email"
                  _placeholder "Email"
                  _autocomplete "email"
                  _required ]
          input [ _type "password"
                  _name "password"
                  _placeholder "Password"
                  _required ]
          input [ _type "submit"
                  _value "Submit" ]
      ] ]
    |> layout

let gitFeed (gits: Git seq) =
    [ ul [] [
          yield! gits |> Seq.map (fun g -> li [] [ str g.GitText ])
      ] ]
    |> layout

let secretView (msg: string) =
    [ partial (); p [] [ encodedText msg ] ] |> layout
