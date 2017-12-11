using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AggregationService.Models.ModelsForView;

namespace AggregationService.Controllers
{
    public class DefaultController : Controller
    {
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
    }
}