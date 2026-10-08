using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClandbusERPIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AcumaticaController : ControllerBase
    {
        private readonly IAcumaticaSessionStore _sessions;

        public AcumaticaController(
            IAcumaticaSessionStore sessions)
        {
            _sessions = sessions;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequestDto request)
        {
            try
            {
                var service = await _sessions.LoginAsync(HttpContext, request);

                if (service is null)
                {
                    return BadRequest(new
                    {
                        message = "ERP login failed"
                    });
                }

                return Ok(new
                {
                    message = "ERP session started successfully",
                    user = new CurrentUserDto(
                        service.CurrentUsername,
                        service.CurrentDisplayName,
                        service.CurrentOwnerId,
                        true)
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    error = "Unexpected integration error"
                });
            }
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            var service = _sessions.GetCurrent(HttpContext);
            return Ok(new CurrentUserDto(service?.CurrentUsername ?? string.Empty,
                service?.CurrentDisplayName ?? string.Empty, service?.CurrentOwnerId ?? string.Empty,
                service?.IsLoggedIn == true));
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var service = _sessions.GetCurrent(HttpContext);
                if (service is null) return Unauthorized();
                var orders = await service.GetLastSalesOrdersAsync();

                return Ok(orders);
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    error = "Unexpected integration error"
                });
            }
        }

        [HttpPost("update-order")]
        public async Task<IActionResult> UpdateOrder(
            UpdateOrderDto request)
        {
            try
            {
                var service = _sessions.GetCurrent(HttpContext);
                if (service is null) return Unauthorized();
                var success = await service.UpdateOrderAsync(request);

                if (!success)
                {
                    return BadRequest(new
                    {
                        message = "Order update failed"
                    });
                }

                return Ok(new
                {
                    message = "Order updated successfully"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    error = "Unexpected integration error"
                });
            }
        }

        [HttpPost("remove-hold")]
        public async Task<IActionResult> RemoveHold(
            RemoveHoldDto request)
        {
            try
            {
                var service = _sessions.GetCurrent(HttpContext);
                if (service is null) return Unauthorized();
                var success = await service.RemoveHoldAsync(request);

                if (!success)
                {
                    return BadRequest(new
                    {
                        message = "Remove Hold failed"
                    });
                }

                return Ok(new
                {
                    message =
                        "Remove Hold executed successfully"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    error = "Unexpected integration error"
                });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _sessions.LogoutAsync(HttpContext);

                return Ok(new
                {
                    message = "Logout successful"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    error = "Unexpected integration error"
                });
            }
        }
    }
}
