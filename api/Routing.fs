namespace DailyDos.Api

open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.IdentityModel.Tokens
open System
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text

open DailyDos.Api.Models

// ---------------------------------
// Models
// ---------------------------------
type Message = { Text: string }

// ---------------------------------
// Views
// ---------------------------------
module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title [] [ encodedText "giraffe" ]
                link [
                    _rel "stylesheet"
                    _type "text/css"
                    _href "/main.css"
                ]
            ]
            body [] content
        ]

    let partial () = h1 [] [ encodedText "giraffe" ]

    let index (model: Message) =
        [ partial ()
          p [] [ encodedText model.Text ] ]
        |> layout

// ---------------------------------
// Web app
// ---------------------------------
module Routing =
    // Authentication example from https://dsincl12.medium.com/json-web-token-with-giraffe-and-f-4cebe1c3ef3b
    let secret = "spadR2dre#u-ruBrE@TepA&*Uf@U"

    let authorize =
        requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

    let generateToken email =
        let claims =
            [| Claim(JwtRegisteredClaimNames.Sub, email)
               Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) |]

        let expires = Nullable(DateTime.UtcNow.AddHours(1.0))
        let notBefore = Nullable(DateTime.UtcNow)

        let securityKey =
            SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))

        let signingCredentials =
            SigningCredentials(key = securityKey, algorithm = SecurityAlgorithms.HmacSha256)

        let token =
            JwtSecurityToken(
                issuer = "jwtwebapp.net",
                audience = "jwtwebapp.net",
                claims = claims,
                expires = expires,
                notBefore = notBefore,
                signingCredentials = signingCredentials
            )

        let tokenResult =
            { token = JwtSecurityTokenHandler().WriteToken(token) }

        tokenResult

    let handleGetSecured =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let email =
                ctx.User.FindFirst ClaimTypes.NameIdentifier

            text
                ("User "
                 + email.Value
                 + " is authorized to access this resource.")
                next
                ctx

    let handlePostToken =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! model = ctx.BindJsonAsync<LoginViewModel>()

                // authenticate user

                let tokenResult = generateToken model.email

                return! json tokenResult next ctx
            }

    let indexHandler (name: string) =
        let greetings = sprintf "Hello %s, from Giraffe!" name
        let model = { Text = greetings }
        let view = Views.index model
        htmlView view

    let webApp: HttpHandler =
        choose [
            GET
            >=> choose [
                    route "/" >=> indexHandler "world"
                    routef "/hello/%s" indexHandler
                    route "/ping"
                    >=> authorize
                    >=> json {| online = true |}
                ]
            POST
            >=> choose [
                    route "/token" >=> handlePostToken
                ]
            setStatusCode 404 >=> text "Not Found"
        ]
