using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/vendors")]
    public class VendorController(ILogger<VendorController> logger) : ControllerBase {
        private readonly ILogger<VendorController> _Logger = logger;

        public record class AddVendorRequest(Vendor Vendor);
        public record class UpdateVendorRequest(Vendor Vendor);
        public record class DeleteVendorRequest(int VendorId);

        [HttpPost]
        [Route("add")]
        public IActionResult Add([FromBody] AddVendorRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a vendor");
                }

                if (request.Vendor.VendorId <= 0) {
                    return BadRequest("Vendor ID is invalid");
                }

                if (string.IsNullOrWhiteSpace(request.Vendor.VendorName)) {
                    return BadRequest("Vendor name is required");
                }

                if (string.IsNullOrWhiteSpace(request.Vendor.Website)) {
                    return BadRequest("Vendor website is required");
                }

                var vendor = WishDatabase.Load().GetVendorById(request.Vendor.VendorId);
                if (vendor != null) {
                    return BadRequest("Vendor id is already in use");
                }

                vendor = WishDatabase.Load().GetVendorByName(request.Vendor.VendorName);
                if (vendor != null) {
                    return BadRequest("Vendor name is already in use");
                }

                WishDatabase.Load().AddVendor(request.Vendor);
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

                if (string.IsNullOrWhiteSpace(request.Vendor.VendorName)) {
                    return BadRequest("Vendor name is required");
                }

                if (string.IsNullOrWhiteSpace(request.Vendor.Website)) {
                    return BadRequest("Vendor website is required");
                }

                var vendor = WishDatabase.Load().GetVendorById(request.Vendor.VendorId);
                if (vendor == null) {
                    return BadRequest("Vendor does not exist");
                }

                vendor = WishDatabase.Load().GetVendorByName(request.Vendor.VendorName);
                if (vendor != null && vendor.VendorId != request.Vendor.VendorId) {
                    return BadRequest("Vendor name is already in use");
                }

                WishDatabase.Load().UpdateVendor(request.Vendor.VendorId, request.Vendor);
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

                if (request.VendorId <= 0) {
                    return BadRequest("Vendor ID is invalid");
                }

                var vendor = WishDatabase.Load().GetVendorById(request.VendorId);
                if (vendor == null) {
                    return BadRequest("Vendor doesn't exists");
                }

                WishDatabase.Load().DeleteVendor(vendor);

                int unspecifiedVendorId = Vendor.GetUnspecified().VendorId;
                foreach (var wishItem in WishDatabase.Load().GetWishes()) {
                    if (wishItem.VendorId == vendor.VendorId) {
                        wishItem.VendorId = unspecifiedVendorId;
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