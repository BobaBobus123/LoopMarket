using System.Collections.Generic;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllCategoriesAsync();
    Task<Category> CreateCategoryAsync(CreateCategoryDto dto);
}
