using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/vendors")]
    public class VendorController(ILogger<VendorController> logger) : ControllerBase {
        private readonly ILogger<VendorController> _Logger = logger;

        public record class AddVendorRequest(string Name, string Website);
        public record class UpdateVendorRequest(string Name, string Website);
        public record class DeleteVendorRequest(string Name);

        [HttpPost]
        [Route("add")]
        public IActionResult Add([FromBody] AddVendorRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a vendor");
                }

                if (string.IsNullOrWhiteSpace(request.Name)) {
                    return BadRequest("Vendor name is required");
                }

                if (string.IsNullOrWhiteSpace(request.Website)) {
                    return BadRequest("Vendor website is required");
                }

                var vendor = WishDatabase.Load().GetVendors().FirstOrDefault(x => x.VendorName == request.Name);
                if (vendor != null) {
                    return BadRequest("Vendor already exists");
                }

                var newVendor = new Vendor(IdGenerator.GetNumericalId(), request.Name, request.Website);
                WishDatabase.Load().AddVendor(newVendor);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to add vendor");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to add vendor");
            }
        }

        [HttpPost]
        [Route("update")]
        public IActionResult Update([FromBody] UpdateVendorRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a vendor");
                }

                if (string.IsNullOrWhiteSpace(request.Name)) {
                    return BadRequest("Vendor name is required");
                }

                if (string.IsNullOrWhiteSpace(request.Website)) {
                    return BadRequest("Vendor website is required");
                }

                var oldVendor = WishDatabase.Load().GetVendors().FirstOrDefault(x => x.VendorName == request.Name);
                if (oldVendor == null) {
                    return BadRequest("Vendor does not exist");
                }

                Vendor newVendor = new(IdGenerator.GetNumericalId(), request.Name, request.Website);
                WishDatabase.Load().UpdateVendor(oldVendor, newVendor);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to update vendor");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update vendor");
            }
        }

        [HttpPost]
        [Route("delete")]
        public IActionResult Delete([FromBody] DeleteVendorRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a vendor");
                }

                if (string.IsNullOrWhiteSpace(request.Name)) {
                    return BadRequest("Vendor name is required");
                }

                var vendor = WishDatabase.Load().GetVendors().FirstOrDefault(x => x.VendorName == request.Name);
                if (vendor == null) {
                    return BadRequest("Vendor doesn't exists");
                }

                WishDatabase.Load().DeleteVendor(vendor);
                foreach (var wishItem in WishDatabase.Load().GetWishes()) {
                    if (wishItem.VendorId == vendor.VendorId) {
                        wishItem.VendorId = Vendor.CreateUnspecified().VendorId;
                        WishDatabase.Load().UpdateWish(wishItem.WishId, wishItem);
                    }
                }

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to delete vendor");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete vendor");
            }
        }
    }
}