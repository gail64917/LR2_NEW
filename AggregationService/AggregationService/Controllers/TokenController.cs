using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AggregationService.Models.AuthorisationService;
using AggregationService.Provider.JWT;


namespace AggregationService.Controllers
{
    [Produces("application/json")]
    [Route("api/Token")]
    [AllowAnonymous]
    public class TokenController : Controller
    {
        [HttpPost]
        public IActionResult Create([FromBody]User user)
        {
            //if (inputModel.Username != "raj" && inputModel.Password != "password")

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
                                .AddClaim("User", userTruly.ID.ToString())
                                .AddExpiry(1)
                                .Build();

                //return Ok(token);
                return Ok(token.Value);
            }
        }
    }
}