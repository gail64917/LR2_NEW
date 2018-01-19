using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AggregationService.Models.AuthorisationService;
using AggregationService.Provider.JWT;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Text;
using AggregationService.Models.ModelsForView;
using static AggregationService.Logger.Logger;

using static RabbitModels.StatisticSender;
using Microsoft.Extensions.Primitives;
using AggregationService.Data;
using Newtonsoft.Json;

namespace AggregationService.Controllers
{
    public class Token
    {
        public string access_token { get; set; }
        public string expires_in { get; set; }
        public string token_type = "bearer";
        public string scope { get; set; }
    }

    public class RequestAccessToken
    {
        public string code { get; set; }
        public string client_id { get; set; }
        public string clientSecret { get; set; }
        public string redirect_uri { get; set; }
        //public string login { get; set; }
        //public string password { get; set; }
    }

    public class RequestGrant
    {
        public string authUrl = "https://localhost:44336/api/token/oauth";
        public string clientID = TokenController.clientIDTrue;
        public string clientSecret = TokenController.clientSecretTrue;
        public string scope = TokenController.scopeTrue;
    }


    [Produces("application/json")]
    [Route("api/Token")]
    [AllowAnonymous]
    public class TokenController : Controller
    {
        public TokenController(StatisticContext context)
        {
            _context = context;
        }

        private readonly StatisticContext _context;

        public static string clientIDTrue = "sdu3nvii4hzp9hs872jodv";
        public static string clientSecretTrue = "zbq93bbod63n.h9d63bjc5djglf8sgvofhsjx-agvtz1397mv6";
        public static string scopeTrue = "khfv98sdh2j37ds76fhj";

        private const string URLAuthorisation = "https://localhost:44387";

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Refresh()
        {
            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            string login = HttpContext.Session.GetString("Login");
            string lastToken = HttpContext.Session.GetString("Token");
            if (login != null && login != "")
            {
                User user = new Models.AuthorisationService.User() { Login = login, LastToken = lastToken };
                var userTruly = DefaultController.privateWeakCheck(user).Result;
                if (userTruly == null)
                {
                    SendStatistic("Token", DateTime.Now, "Refresh Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    return Unauthorized();
                }

                else
                {
                    var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(userTruly.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                                .AddExpiry(200)
                                .Build();
                    HttpContext.Session.SetString("Token", token.Value);
                    HttpContext.Session.SetString("Login", user.Login);

                    //пихаем новый токен пользователю в бд
                    var values = new JObject();
                    values.Add("id", userTruly.ID);
                    values.Add("login", userTruly.Login);
                    values.Add("password", userTruly.Password);
                    values.Add("role", userTruly.Role);
                    values.Add("lasttoken", token.Value);

                    /**/
                    var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                    /**/
                    string request;
                    /**/
                    string requestMessage = values.ToString();
                    /**/
                    byte[] responseMessage;

                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(URLAuthorisation);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

                    /**/
                    string requestString = "api/users/" + userTruly.ID;

                    var response = await client.PutAsJsonAsync(requestString, values);

                    /**/
                    request = "SERVICE: AuthorisationService \r\nPUT: " + URLAuthorisation + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                    /**/
                    string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                    if ((int)response.StatusCode == 500)
                    {
                        string description = "There is no user with ID (" + user.ID + ")";
                        ResponseMessage message = new ResponseMessage();
                        message.description = description;
                        message.message = response;
                        SendStatistic("Token", DateTime.Now, "Refresh Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                        return Unauthorized();
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        /**/
                        responseMessage = await response.Content.ReadAsByteArrayAsync();
                        /**/
                        await LogQuery(request, requestMessage, responseString, responseMessage);
                    }
                    else
                    {
                        /**/
                        responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                        /**/
                        await LogQuery(request, requestMessage, responseString, responseMessage);

                        SendStatistic("Token", DateTime.Now, "Refresh Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                        return Unauthorized();
                    }
                    SendStatistic("Token", DateTime.Now, "Refresh Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                    return Ok(token.Value);
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        //____________________________________________1____________________________________________
        //Получаем client_id и scope, а также адрес, куда это кидать
        //
        //GET api/token/geturl
        [Route("geturl")]
        public IActionResult GetUrl()
        {
            RequestGrant rg = new RequestGrant();
            return Ok(rg);
        }

        //____________________________________________2____________________________________________
        //Запрашиваем права, кидая client_id и scope и получая страницу с авторизацией
        //GET api/token/oauth
        [Route("oauth")]
        [HttpGet]
        public IActionResult GetAccess([FromQuery] string client_id, [FromQuery] string scope)
        {
            if (clientIDTrue.ToString() != client_id || scopeTrue != scope)
            {
                return Unauthorized();
            }
            return View();
        }


        //________________________________________________________________________4______________________________
        //меняем код на токен доступа, если все правильно
        //POST api/token/requestToken
        [Route("RequestToken")]
        [HttpPost]
        //public async Task<RedirectResult> RequestTokenToAccessToken([FromBody] RequestAccessToken rat)
        public async Task<IActionResult> RequestTokenToAccessToken([FromBody] RequestAccessToken rat)
        {
            //запрос из бд UserData по code, clientID и clientSecret
            UserData result = new UserData();
            int count = 0;
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://localhost:44336");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/userdatas/getbycode?code=" + rat.code;
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: AggregationService \r\nGET: " + "https://localhost:44336" + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var datas = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<UserData>(datas);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    string user2 = HttpContext.Session.GetString("Login");
                    user2 = user2 != null ? user2 : "";
                    SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user2);
                    return Unauthorized();
                    //return Redirect("");
                }
                await LogQuery(request, responseString, responseMessage);
            }
            var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(result.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(result.Role, result.ID.ToString())
                                .AddExpiry(200)
                                .Build();
            HttpContext.Session.SetString("Token", token.Value);
            //string token = HttpContext.Session.GetString("Token");
            //return Ok(token);
            //Token CurrentToken = new Token() { access_token = token.Value, expires_in = "", token_type = "", scope = "" };
            if (rat.clientSecret == result.clientSecret && rat.client_id == result.clientID && rat.code == result.Code)
            {
                string uri = "?access_token=" + token.Value + "&token_type=bearer" + "&scope=" + "khfv98sdh2j37ds76fhj" + "&expire_time=" + token.ValidTo.ToString();
                return Ok(uri);
                //return Redirect(uri);
            }
            else
            {
                return Unauthorized();
                //return Redirect("");
            }
        }



        //GET api/token/token
        [Route("token")]
        [HttpGet]
        public IActionResult Token(string expires, string login)
        {
            string token = HttpContext.Session.GetString("Token");
            //return Ok(token);
            Token CurrentToken = new Token() { access_token = token, expires_in = "", token_type = "", scope ="" };
            //string uri = "?access_token=" + token + "&expires_in=" + expires + "&token_type=bearer&scope="+scopeTrue;
            string code = "";
            if (login == "sad")
                code = "das64917";
            else if (login == "admin")
                code = "nimda64917";
            string uri =  "?code=" + code + "&method=POST&uri=api/token/requestToken" + "?login=" + login + "?client_id" + clientIDTrue + "?clientSecret" + clientSecretTrue + "?expires" + expires; ;
            return Ok(uri);
        }

        //________________________________________________________________2.5_____________________________
        //на успешную авторизацию выдаем код
        [Route("oauth")]
        [HttpPost]
        public async Task<IActionResult> CreateFromView([Bind("Login, Password")]User user)
        {
            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            var userTruly = DefaultController.privateCheck(user).Result;
            if (userTruly == null)
            {
                SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                return Unauthorized();
            }
            else
            {
                UserData result = new UserData();
                int count = 0;
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                string request;
                byte[] responseMessage;
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://localhost:44336");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    string requestString = "api/userdatas/getbylogin?login=" + userTruly.Login;
                    HttpResponseMessage response = await client.GetAsync(requestString);
                    request = "SERVICE: AggregationService \r\nGET: " + "https://localhost:44336" + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                    string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                    if (response.IsSuccessStatusCode)
                    {
                        responseMessage = await response.Content.ReadAsByteArrayAsync();
                        var datas = await response.Content.ReadAsStringAsync();
                        result = JsonConvert.DeserializeObject<UserData>(datas);
                    }
                    else
                    {
                        responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                        string user2 = HttpContext.Session.GetString("Login");
                        user2 = user2 != null ? user2 : "";
                        SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user2);
                        return BadRequest("Service unavailable");
                    }
                    await LogQuery(request, responseString, responseMessage);
                }
                RequestAccessToken rat = new RequestAccessToken() { clientSecret = clientSecretTrue, client_id = clientIDTrue, code = result.Code, redirect_uri = "api/token/requestToken" };
                return Ok(rat);


                //var token = new JwtTokenBuilder()
                //                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                //                .AddSubject(userTruly.Login)
                //                .AddIssuer("Test.Security.Bearer")
                //                .AddAudience("Test.Security.Bearer")
                //                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                //                .AddExpiry(200)
                //                .Build();
                //HttpContext.Session.SetString("Token", token.Value);
                //HttpContext.Session.SetString("Login", user.Login);

                ////пихаем новый токен пользователю в бд
                //var values = new JObject();
                //values.Add("id", userTruly.ID);
                //values.Add("login", userTruly.Login);
                //values.Add("password", userTruly.Password);
                //values.Add("role", userTruly.Role);
                //values.Add("lasttoken", token.Value);

                ///**/
                //var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                ///**/
                //string request;
                ///**/
                //string requestMessage = values.ToString();
                ///**/
                //byte[] responseMessage;

                //HttpClient client = new HttpClient();
                //client.BaseAddress = new Uri(URLAuthorisation);
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

                ///**/
                //string requestString = "api/users/" + userTruly.ID;

                //var response = await client.PutAsJsonAsync(requestString, values);

                ///**/
                //request = "SERVICE: AuthorisationService \r\nPUT: " + URLAuthorisation + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                ///**/
                //string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                //if ((int)response.StatusCode == 500)
                //{
                //    string description = "There is no user with ID (" + user.ID + ")";
                //    ResponseMessage message = new ResponseMessage();
                //    message.description = description;
                //    message.message = response;
                //    SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                //    return Unauthorized();
                //}

                //if (response.IsSuccessStatusCode)
                //{
                //    /**/
                //    responseMessage = await response.Content.ReadAsByteArrayAsync();
                //    /**/
                //    await LogQuery(request, requestMessage, responseString, responseMessage);
                //}
                //else
                //{
                //    /**/
                //    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                //    /**/
                //    await LogQuery(request, requestMessage, responseString, responseMessage);
                //    SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                //    return Unauthorized();
                //}
                //SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                //return Ok(token.Value);
                //return RedirectToAction("Token", new { expires = token.ValidTo.ToString() , login = userTruly.Login } );
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody]User user)
        {
            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            var userTruly = DefaultController.privateCheck(user).Result;
            if (userTruly == null)
            {
                SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                return Unauthorized();
            }
            else
            {
                var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(userTruly.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                                .AddExpiry(200)
                                .Build();
                HttpContext.Session.SetString("Token", token.Value);
                HttpContext.Session.SetString("Login", user.Login);

                //пихаем новый токен пользователю в бд
                var values = new JObject();
                values.Add("id", userTruly.ID);
                values.Add("login", userTruly.Login);
                values.Add("password", userTruly.Password);
                values.Add("role", userTruly.Role);
                values.Add("lasttoken", token.Value);

                /**/
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                /**/
                string request;
                /**/
                string requestMessage = values.ToString();
                /**/
                byte[] responseMessage;

                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(URLAuthorisation);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

                /**/
                string requestString = "api/users/" + userTruly.ID;

                var response = await client.PutAsJsonAsync(requestString, values);

                /**/
                request = "SERVICE: AuthorisationService \r\nPUT: " + URLAuthorisation + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                /**/
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                if ((int)response.StatusCode == 500)
                {
                    string description = "There is no user with ID (" + user.ID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    return Unauthorized();
                }

                if (response.IsSuccessStatusCode)
                {
                    /**/
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    /**/
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                }
                else
                {
                    /**/
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    /**/
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    return Unauthorized();
                }
                SendStatistic("Token", DateTime.Now, "Create Token", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                return Ok(token.Value);
            }
        }
    }
}