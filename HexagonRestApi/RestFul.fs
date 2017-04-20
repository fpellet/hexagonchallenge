﻿namespace HexagonRestApi.Rest

module RestFul =
  open Newtonsoft.Json
  open Newtonsoft.Json.Serialization
  open Suave.Successful
  open Suave
  open Suave.Operators 
  open Suave.Filters
  open Suave.Successful
  open Suave.RequestErrors
  open HexagonRestApi.AisService
  open HexagonRestApi.Domain.Domain
    
  type RestResource<'a> = {
    Submit : 'a -> 'a
    GetById : 'a -> 'a option
  }

  let JSON objectToSerialize =
    let jsonSerializerSettings = new JsonSerializerSettings()
    jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()
    JsonConvert.SerializeObject(objectToSerialize, jsonSerializerSettings) 
    |> OK
    >=> Writers.setMimeType("application/json; charset=utf-8")

  let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

  let getResourceFromRequest<'a> (req : HttpRequest) =
    let getString rawForm =
        System.Text.Encoding.UTF8.GetString(rawForm)
    req.rawForm |> getString |> fromJson<'a>


  let aiRest (submit: Ai -> unit) (tryToGetCode: Ai -> string option) =    
    let errorIfNone = function
        | Some r -> r |> OK
        | _ -> NOT_FOUND "Resource not found"

    choose [
        path "/ais" >=> POST >=> request (getResourceFromRequest >> submit >> (fun () -> "Saved" |> OK))
        path "/ais/get"  >=> POST >=> request (getResourceFromRequest >> tryToGetCode >> errorIfNone)
        ]
     

