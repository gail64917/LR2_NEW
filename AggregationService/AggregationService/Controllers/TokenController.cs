using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

namespace AggregationService.Controllers
{
    [Produces("application/json")]
    [Route("api/Token")]
    [AllowAnonymous]
    public class TokenController : Controller
    {
        private const string URLAuthorisation = "http://localhost:59917";

        [HttpPost]
        public async Task<IActionResult> Create([FromBody]User user)
        {
            var userTruly = DefaultController.privateCheck(user).Result;
            if (userTruly == null)
                return Unauthorized();
            else
            {
                var token = new JwtTokenBuilder()
                                .AddSecurityKey(JwtSecurityKey.Create("Test-secret-key-1234"))
                                .AddSubject(userTruly.Login)
                                .AddIssuer("Test.Security.Bearer")
                                .AddAudience("Test.Security.Bearer")
                                .AddClaim(userTruly.Role, userTruly.ID.ToString())
                                .AddExpiry(1)
                                .Build();

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
                }

                return Ok(token.Value);
            }
        }
    }
}