using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AggregationService.Models.ModelsForView;
using AggregationService.Models.AuthorisationService;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using static AggregationService.Logger.Logger;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using AggregationService.Provider.JWT;
using Microsoft.AspNetCore.Http;

namespace AggregationService.Controllers
{
    public class DefaultController : Controller
    {
        private const string URLAuthorisationService = "https://localhost:44387";
        
        [HttpGet("{id?}")]
        public IActionResult Index(int? i)
        {
            return View();
        }

        [Route("Info")]
        public IActionResult Comment(string comment)
        {
            StringView sv = new StringView() { comment = comment.Split("\r\n").ToList() };
            return View(sv);
        }

        [Route("LogOut")]
        public IActionResult LogOut()
        {
            HttpContext.Session.SetString("Token", "");
            HttpContext.Session.SetString("Login", "");
            return RedirectToAction(nameof(Index));
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}

        [Route("Registration")]
        public IActionResult Registration()
        {
            return View();
        }

        [Route("Registration")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registration([Bind("Login, Password")] User user)
        {
            var values = new JObject();
            values.Add("Login", user.Login);
            values.Add("Password", user.Password);
            values.Add("Role", "User");

            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLAuthorisationService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            string requestString = "api/Users";

            var response = await client.PostAsJsonAsync(requestString, values);

            if ((int)response.StatusCode == 500)
            {
                string description = "Cannot create user";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }

            request = "SERVICE: AuthorisationService \r\nPOST: " + URLAuthorisationService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                return RedirectToAction(nameof(Index));
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Another error ";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
        }

        [Route("Authorisation")]
        public IActionResult Authorisation()
        {
            return View();
        }
                
        public async Task<IActionResult> privateAuth(User user)
        {
            var values = new JObject();
            values.Add("Login", user.Login);
            values.Add("Password", user.Password);

            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLAuthorisationService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            string requestString = "api/Users/Find";

            var response = await client.PostAsJsonAsync(requestString, values);

            if ((int)response.StatusCode == 500)
            {
                string description = "Cannot find user";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }

            request = "SERVICE: AuthorisationService \r\nPOST: " + URLAuthorisationService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                if ((int)response.StatusCode == 204)
                {
                    string description = "There is no user like this";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var userJson = await response.Content.ReadAsStringAsync();
                var userTruly = JsonConvert.DeserializeObject<User>(userJson);
                var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(userTruly.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                                .AddExpiry(182)
                                .Build();

                //return Ok(token.Value);
                HttpContext.Session.SetString("Token", token.Value);
                HttpContext.Session.SetString("Login", userTruly.Login);
                return RedirectToAction(nameof(Index));
            }
            
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Another error ";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
        }

        public static async Task<User> privateCheck(User user)
        {
            var values = new JObject();
            values.Add("Login", user.Login);
            values.Add("Password", user.Password);

            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLAuthorisationService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            string requestString = "api/Users/Find";

            var response = await client.PostAsJsonAsync(requestString, values);

            if ((int)response.StatusCode == 500)
            {
                string description = "Cannot find user";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return null;
            }

            request = "SERVICE: AuthorisationService \r\nPOST: " + URLAuthorisationService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                if ((int)response.StatusCode == 204)
                {
                    string description = "There is no user like this";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return null;
                }
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var userJson = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<User>(userJson);
                return result;
            }

            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Another error ";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return null;
            }
        }

        [Route("Authorisation")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Authoristation([Bind("Login, Password")] User user)
        {
            return await privateAuth(user);
        }
    }
}