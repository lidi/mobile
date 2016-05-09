#r "System.Net.Http"
#r "../../packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll"

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Threading.Tasks
open Newtonsoft.Json.Linq

let sendNotification(apiKey: string, message: string) =
    let jGcmData = new JObject()
    let jData = new JObject()

    jData.Add ("message", unbox message)
    jGcmData.Add ("to", unbox "/topics/global")
    jGcmData.Add ("data", jData)

    let url = new Uri ("https://gcm-http.googleapis.com/gcm/send")
    try
        use client = new HttpClient()

        client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"))

        client.DefaultRequestHeaders.TryAddWithoutValidation (
            "Authorization", "key=" + apiKey)
        |> ignore

        async {
            let content = new StringContent(jGcmData.ToString(), Encoding.Default, "application/json")
            let! response = Async.AwaitTask <| client.PostAsync(url, content)
            Console.WriteLine(response)
            Console.WriteLine("Message sent: check the client device notification tray.")
        }
        |> Async.RunSynchronously
    with
    | e ->
        Console.WriteLine("Unable to send GCM message:")
        Console.Error.WriteLine(e.StackTrace)
        |> ignore

let API_KEY = "API_KEY"
let MESSAGE = "Hello, Xamarin!"

sendNotification(API_KEY, MESSAGE)

