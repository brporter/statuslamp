namespace StatusService.Controllers
{
    using System;
    using Microsoft.AspNetCore.Mvc;
    using StatusService.Services;
    using StatusService.Models;

    [Route("api/[controller]")]
    public class StatusController 
        : Controller
    {
        readonly IStatusService _statusRepo;

        private StatusController() { }

        public StatusController(IStatusService statusRepo)
        {
            _statusRepo = statusRepo;
        }

        // GET api/status/5
        [HttpGet("{id:guid}")]
        public DeviceStatus Get(Guid id)
        {
            return new DeviceStatus() {
                DeviceIdentifier = id,
                Status = _statusRepo.GetStatus(id) ?? "Unknown"
            };
        }

        // PUT api/status
        [HttpPut()]
        public void Put([FromBody]DeviceStatus status)
        {
            _statusRepo.SetStatus(status.DeviceIdentifier, status.Status);
        }
    }
}
