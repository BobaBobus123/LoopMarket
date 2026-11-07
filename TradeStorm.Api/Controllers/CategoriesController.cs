using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly AppDbContext _context;

    public CategoriesController(ICategoryService categoryService, AppDbContext context)
    {
        _categoryService = categoryService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var rootCategories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Icon,
                subcategories = c.Subcategories
                    .OrderBy(s => s.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Icon,
                        s.ParentId
                    }).ToList()
            })
            .ToListAsync();

        return Ok(rootCategories);
    }

    [HttpGet("flat")]
    public async Task<IActionResult> GetFlatCategories()
    {
        var all = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.ParentId ?? c.Id)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Icon,
                c.ParentId
            })
            .ToListAsync();

        return Ok(all);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var category = await _categoryService.CreateCategoryAsync(dto);
        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, category);
    }
}
