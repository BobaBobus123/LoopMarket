using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Repositories;

namespace TradeStorm.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _categoryRepository;

    public CategoryService(IRepository<Category> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<Category>> GetAllCategoriesAsync()
    {
        return await _categoryRepository.GetAllAsync();
    }

    public async Task<Category> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var category = new Category { Name = dto.Name.Trim() };
        return await _categoryRepository.AddAsync(category);
    }
}
