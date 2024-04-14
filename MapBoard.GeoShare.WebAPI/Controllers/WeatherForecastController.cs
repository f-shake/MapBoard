using MapBoard.GeoShare.Core.Entity;
using MapBoard.GeoShare.Core.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Threading.Tasks;

namespace MapBoard.GeoShare.WebAPI.Controllers
{
    public class GeoShareControllerBase : ControllerBase
    {
        protected int GetUser()
        {
            if(HttpContext.Session.TryGetValue("user",out byte[] userIdBytes))
            {
               return Convert.ToInt32(userIdBytes);
            }
            throw new Exception("δ��¼");
        }
    }
    [ApiController]
    [Route("[controller]")]
    public class UserController(UserService userService) : GeoShareControllerBase
    {
        private readonly UserService userService = userService;

        [HttpPost]
        public async Task<IActionResult> Verify(UserEntity user)
        {
            var dbUser = await userService.GetUserAsync(user.Username);
            if(dbUser == null)
            {
                throw new Exception("�û�������");
            }
            if(dbUser.Password!=user.Password)
            {
                throw new Exception("�û��������벻ƥ��");
            }
            HttpContext.Session.SetInt32("user", dbUser.Id) ;
            return Ok();
        }
    }
    [ApiController]
    [Route("[controller]")]
    public class SharedLocationController (SharedLocationService sharedLocationService): GeoShareControllerBase
    {
        private readonly SharedLocationService sharedLocationService = sharedLocationService;

        [HttpGet]
        public Task<IActionResult> GetLatestLocations()
        {
            throw new Exception("dasd");
            //return sharedLocationService.GetGroupLastLocationAsync(GetUser());
        }
    }
}
