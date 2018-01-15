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

using static RabbitModels.StatisticSender;

namespace AggregationService.Controllers
{
    public class DefaultController : Controller
    {
        public class VisitingData
        {
            public string user { get; set; }
            public int visits { get; set; }
        }


        private const string URLAuthorisationService = "https://localhost:44387";
        private const string URLStatisticService = "https://localhost:44315";

        [HttpGet("{id?}")]
        public IActionResult Index(int? i)
        {
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Default", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
            return View();
        }

        [Route("Info")]
        public IActionResult Comment(string comment)
        {
            StringView sv = new StringView() { comment = comment.Split("\r\n").ToList() };
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Default", DateTime.Now, "Info", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
            return View(sv);
        }

        [Route("LogOut")]
        public IActionResult LogOut()
        {
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Default", DateTime.Now, "LogOut", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
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
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Default", DateTime.Now, "Registration Start", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Default", DateTime.Now, "Registration Ending", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return View("Error", message);
            }

            request = "SERVICE: AuthorisationService \r\nPOST: " + URLAuthorisationService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Default", DateTime.Now, "Registration Ending", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Default", DateTime.Now, "Registration Ending", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
                return View("Error", message);
            }
        }

        [Route("Authorisation")]
        public IActionResult Authorisation()
        {
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Default", DateTime.Now, "Authorisation Start", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
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
                string user25 = HttpContext.Session.GetString("Login");
                user25 = user25 != null ? user25 : "";
                SendStatistic("Default", DateTime.Now, "Authorisation Processing", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user25);
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
                    string user24 = HttpContext.Session.GetString("Login");
                    user24 = user24 != null ? user24 : "";
                    SendStatistic("Default", DateTime.Now, "Authorisation Processing", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user24);
                    return View("Error", message);
                }
                await LogQuery(request, requestMessage, responseString, responseMessage);

                //take token
                var userJson = await response.Content.ReadAsStringAsync();
                var userTruly = JsonConvert.DeserializeObject<User>(userJson);
                var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(userTruly.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                                .AddExpiry(200)
                                .Build();

                //return Ok(token.Value);
                HttpContext.Session.SetString("Token", token.Value);
                HttpContext.Session.SetString("Login", userTruly.Login);
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Default", DateTime.Now, "Authorisation Processing", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Default", DateTime.Now, "Authorisation Processing", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
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

        public static async Task<User> privateWeakCheck(User user)
        {
            var values = new JObject();
            values.Add("Login", user.Login);
            values.Add("LastToken", user.LastToken);

            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLAuthorisationService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            string requestString = "api/Users/FindByLogin";

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
            string user1 = HttpContext.Session.GetString("Login");
            user1 = user1 != null ? user1 : "";
            SendStatistic("Default", DateTime.Now, "Authorisation Ends", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
            return await privateAuth(user);
        }


        [Route("Statistic")]
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Statistic()
        {
            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            StatisticFake statfake = new StatisticFake();

            List<RabbitModels.RabbitStatistic> result = new List<RabbitModels.RabbitStatistic>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLStatisticService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/statistics";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLStatisticService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<RabbitModels.RabbitStatistic>>(json);
                    statfake.statistic = result;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    return BadRequest();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
                SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                return View(statfake);
            }
        }

        [Route("Statistic2")]
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Statistic2()
        {
            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            StatisticFake statfake = new StatisticFake();

            List<RabbitModels.RabbitStatistic> result = new List<RabbitModels.RabbitStatistic>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLStatisticService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/statistics/FromQueue";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLStatisticService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<RabbitModels.RabbitStatistic>>(json);
                    statfake.statistic = result;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    return BadRequest();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
                SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                return View(statfake);
            }
        }

        [Route("GetData")]
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IEnumerable<VisitingData>> GetData()
        {
            //Dictionary<string, int> data = new Dictionary<string, int>();
            List<VisitingData> data = new List<VisitingData>();

            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            StatisticFake statfake = new StatisticFake();

            List<RabbitModels.RabbitStatistic> result = new List<RabbitModels.RabbitStatistic>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLStatisticService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/statistics";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLStatisticService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<RabbitModels.RabbitStatistic>>(json);
                    statfake.statistic = result;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    SendStatistic("Default", DateTime.Now, "GetData", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    //return BadRequest();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
                SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                //return View(statfake);
            }
            foreach (RabbitModels.RabbitStatistic item in result)
            {
                bool found = false;
                foreach (VisitingData dataItem in data)
                {
                    if (item.User == dataItem.user)
                    {
                        dataItem.visits++;
                        found = true;
                    }
                }
                if (!found)
                {
                    VisitingData dataItem = new VisitingData() { user = item.User, visits = 1 };
                    data.Add(dataItem);
                }
            }

            var chartData = new object[data.Count + 1];
            chartData[0] = new object[]
            {
                "user",
                "visits"
            };
            int j = 0;
            foreach (var i in data)
            {
                j++;
                chartData[j] = new object[] { i.user, i.visits };
            }

            //return new JsonResult(chartData);
            return data;
        }

        [Route("GetAnotherData")]
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IEnumerable<VisitingData>> GetAnotherData()
        {
            //Dictionary<string, int> data = new Dictionary<string, int>();
            List<VisitingData> data = new List<VisitingData>();

            string userString = HttpContext.Session.GetString("Login");
            userString = userString != null ? userString : "";

            StatisticFake statfake = new StatisticFake();

            List<RabbitModels.RabbitStatistic> result = new List<RabbitModels.RabbitStatistic>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLStatisticService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/statistics";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLStatisticService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<RabbitModels.RabbitStatistic>>(json);
                    statfake.statistic = result;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    SendStatistic("Default", DateTime.Now, "GetData", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, userString);
                    //return BadRequest();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
                SendStatistic("Default", DateTime.Now, "Statistic", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, userString);
                //return View(statfake);
            }
            foreach (RabbitModels.RabbitStatistic item in result)
            {
                if (item.Action == "Index")
                {
                    bool found = false;
                    foreach (VisitingData dataItem in data)
                    {
                        if (item.User == dataItem.user)
                        {
                            dataItem.visits++;
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        VisitingData dataItem = new VisitingData() { user = item.User, visits = 1 };
                        data.Add(dataItem);
                    }
                }
            }

            var chartData = new object[data.Count + 1];
            chartData[0] = new object[]
            {
                "user",
                "visits"
            };
            int j = 0;
            foreach (var i in data)
            {
                j++;
                chartData[j] = new object[] { i.user, i.visits };
            }

            //return new JsonResult(chartData);
            return data;
        }

    }
}