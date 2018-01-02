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

namespace AggregationService.Controllers
{
    public class DefaultController : Controller
    {
        private const string URLAuthorisationService = "http://localhost:59917";

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

        [Route("Authorisation")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Authoristation([Bind("Login, Password")] User user)
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
    }
}