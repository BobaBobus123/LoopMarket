using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Repositories;

namespace TradeStorm.Api.Services;

public class CategoryService : ICategoryService
{
    private const string CategoriesCacheKey = "loopmarket_all_categories_v1";
    private readonly IRepository<Category> _categoryRepository;
    private readonly IMemoryCache? _cache;

    public CategoryService(IRepository<Category> categoryRepository, IMemoryCache? cache = null)
    {
        _categoryRepository = categoryRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Category>> GetAllCategoriesAsync()
    {
        if (_cache != null && _cache.TryGetValue(CategoriesCacheKey, out IReadOnlyList<Category>? cached) && cached != null)
        {
            return cached;
        }

        var result = await _categoryRepository.GetAllAsync();
        _cache?.Set(CategoriesCacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<Category> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var category = new Category { Name = dto.Name.Trim() };
        var created = await _categoryRepository.AddAsync(category);
        _cache?.Remove(CategoriesCacheKey);
        return created;
    }
}
