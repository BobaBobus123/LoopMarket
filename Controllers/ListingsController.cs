using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ListingsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetListings([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Seller)
            .AsNoTracking();

        var totalItems = await query.CountAsync();

        var listings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { totalItems, page, pageSize, listings });
    }

    [HttpPost]
    public async Task<IActionResult> CreateListing(CreateListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var listing = new Listing
        {
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            CategoryId = dto.CategoryId,
            SellerId = dto.SellerId
        };

        _context.Listings.Add(listing);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetListings), new { id = listing.Id }, listing);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateListing(int id, UpdateListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var listing = await _context.Listings.FindAsync(id);
        if (listing == null) return NotFound();

        listing.Title = dto.Title;
        listing.Description = dto.Description;
        listing.Price = dto.Price;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteListing(int id)
    {
        var listing = await _context.Listings.FindAsync(id);
        if (listing == null) return NotFound();

        _context.Listings.Remove(listing);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}