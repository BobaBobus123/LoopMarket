using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Models;

namespace TradeStorm.Api.Data;

public static class CategorySeeder
{
    public static void SeedHierarchicalCategories(AppDbContext db)
    {
        var categoryTree = new List<(string Name, string Icon, List<(string Name, string Icon)> Subs)>
        {
            ("Електроніка та техніка", "📱", new()
            {
                ("Смартфони та телефони", "📱"),
                ("Ноутбуки та ПК", "💻"),
                ("Навушники та аудіо", "🎧"),
                ("Телевізори та відео", "📺"),
                ("Смарт-годинники", "⌚"),
                ("Ігрові консолі та геймінг", "🎮"),
                ("Планшети та рідери", "📟")
            }),
            ("Автономне живлення та енергія", "⚡", new()
            {
                ("Зарядні станції (EcoFlow / Bluetti)", "🔋"),
                ("Генератори інверторні та бензинові", "🔌"),
                ("Павербанки та швидка зарядка", "⚡"),
                ("Сонячні панелі та контролери", "☀️"),
                ("Інвертори та ДБЖ (UPS)", "🔄"),
                ("Акумулятори LiFePO4 / AGM", "🪫")
            }),
            ("Військторг та тактичне спорядження", "🛡️", new()
            {
                ("Тактична амуніція та плитоноски", "🦺"),
                ("Берці та тактичне взуття", "🥾"),
                ("Тактичні рюкзаки та підсумки", "🎒"),
                ("Ліхтарі та автономне світло", "🔦"),
                ("Оптика, біноклі та ПНБ", "🔭"),
                ("Тактична медицина (IFAK / турнікети)", "🩹")
            }),
            ("Одяг, взуття та аксесуари", "👗", new()
            {
                ("Чоловічий одяг", "👔"),
                ("Жіночий одяг", "👗"),
                ("Взуття та кросівки", "👟"),
                ("Сумки, рюкзаки та валізи", "👜"),
                ("Аксесуари та годинники", "👓"),
                ("Дитячий одяг та взуття", "👶")
            }),
            ("Товари для дому та затишку", "🏠", new()
            {
                ("Меблі та дизайн", "🛋️"),
                ("Текстиль та постільна білизна", "🛏️"),
                ("Посуд та кухонне приладдя", "🍽️"),
                ("Освітлення та смарт-лампи", "💡"),
                ("Рослини та садівництво", "🌱"),
                ("Побутова хімія та прибирання", "🧹")
            }),
            ("Будівництво, ремонт та інструменти", "🛠️", new()
            {
                ("Електроінструмент", "🪚"),
                ("Ручний інструмент", "🔧"),
                ("Сантехніка та водопостачання", "🚿"),
                ("Будівельні матеріали", "🧱"),
                ("Фарби, лаки та герметики", "🎨"),
                ("Двері, замки та фурнітура", "🚪")
            }),
            ("Краса, здоров'я та догляд", "💄", new()
            {
                ("Парфумерія", "🌸"),
                ("Догляд за шкірою та обличчям", "🧴"),
                ("Догляд за волоссям", "💇"),
                ("Вітаміни та біодобавки", "💊"),
                ("Медичні прилади та термометри", "🩺")
            }),
            ("Транспорт та автотовари", "🚗", new()
            {
                ("Автозапчастини", "⚙️"),
                ("Шини та диски", "🛞"),
                ("Автохімія та оливи", "🧴"),
                ("Автоелектроніка та навігація", "📻"),
                ("Велосипеди та електросамокати", "🚲")
            }),
            ("Навчальні матеріали, книги та хобі", "📚", new()
            {
                ("Художня література", "📖"),
                ("Бізнес, психологія та розвиток", "💼"),
                ("Настільні ігри та головоломки", "🎲"),
                ("Творчість, малювання та хобі", "🎨"),
                ("Канцелярія та шкільні товари", "✏️")
            }),
            ("Дитячий світ", "🧸", new()
            {
                ("Іграшки та конструктори", "🧸"),
                ("Дитяче харчування та догляд", "🍼"),
                ("Коляски та автокрісла", "🚼"),
                ("Дитячі меблі та ліжечка", "🛏️")
            }),
            ("Спорт, хобі та активний відпочинок", "⚽", new()
            {
                ("Кемпінг, намети та туризм", "🏕️"),
                ("Тренажери та фітнес", "🏋️"),
                ("Риболовля та полювання", "🎣"),
                ("Водний спорт та плавання", "🤿")
            }),
            ("Зоотовари", "🐾", new()
            {
                ("Корми та ласощі", "🥩"),
                ("Амуніція та нашийники", "🦮"),
                ("Лежанки, кігтеточки та клітки", "🛋️"),
                ("Ветаптека та догляд", "💊")
            }),
            ("Послуги та робота", "💼", new()
            {
                ("СТО та автопослуги", "🚗"),
                ("Ремонт житла, електрика та сантехніка", "🔨"),
                ("IT, сайти та графічний дизайн", "💻"),
                ("Вантажні перевезення та вантажники", "🚚"),
                ("Репетиторство та вивчення мов", "🎓"),
                ("Клінінг та прибирання", "🧹")
            }),
            ("Нерухомість", "🏢", new()
            {
                ("Довгострокова оренда квартир", "🔑"),
                ("Продаж квартир", "🏷️"),
                ("Будинки, котеджі та дачі", "🏡"),
                ("Комерційна нерухомість", "🏬"),
                ("Земельні ділянки", "🌾")
            }),
            ("Безкоштовно та обмін", "🎁", new()
            {
                ("Віддам безкоштовно", "🎁"),
                ("Обмін товарами", "🔄")
            }),
            ("Інше", "📦", new()
            {
                ("Різні товари", "📦")
            })
        };

        var existingCategories = db.Categories.ToList();

        foreach (var def in categoryTree)
        {
            // Пошук головної категорії (або за точною назвою, або за початком)
            var root = existingCategories.FirstOrDefault(c => 
                c.ParentId == null && (
                    c.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase) ||
                    (def.Name.StartsWith("Електрон") && c.Name.StartsWith("Електрон")) ||
                    (def.Name.StartsWith("Одяг") && c.Name.StartsWith("Одяг")) ||
                    (def.Name.StartsWith("Товари для дому") && c.Name.StartsWith("Товари для дому")) ||
                    (def.Name.StartsWith("Навчальн") && c.Name.StartsWith("Навчальн")) ||
                    (def.Name.StartsWith("Спорт") && c.Name.StartsWith("Спорт")) ||
                    (def.Name.StartsWith("Зоо") && c.Name.StartsWith("Зоо")) ||
                    (def.Name.StartsWith("Транспорт") && c.Name.StartsWith("Транспорт")) ||
                    (def.Name.StartsWith("Послуги") && c.Name.StartsWith("Послуги")) ||
                    (def.Name.StartsWith("Безкоштовно") && c.Name.StartsWith("Безкоштовно")) ||
                    (def.Name.StartsWith("Інше") && c.Name.StartsWith("Інше"))
                ));

            if (root == null)
            {
                root = new Category
                {
                    Name = def.Name,
                    Icon = def.Icon,
                    ParentId = null
                };
                db.Categories.Add(root);
                db.SaveChanges();
                existingCategories.Add(root);
            }
            else
            {
                bool modified = false;
                if (string.IsNullOrEmpty(root.Icon) || root.Icon != def.Icon)
                {
                    root.Icon = def.Icon;
                    modified = true;
                }
                if (root.ParentId != null)
                {
                    root.ParentId = null;
                    modified = true;
                }
                if (modified)
                {
                    db.SaveChanges();
                }
            }

            // Додаємо підкатегорії
            foreach (var sub in def.Subs)
            {
                var existingSub = existingCategories.FirstOrDefault(c => 
                    c.ParentId == root.Id && c.Name.Equals(sub.Name, StringComparison.OrdinalIgnoreCase));

                if (existingSub == null)
                {
                    var newSub = new Category
                    {
                        Name = sub.Name,
                        Icon = sub.Icon,
                        ParentId = root.Id
                    };
                    db.Categories.Add(newSub);
                    db.SaveChanges();
                    existingCategories.Add(newSub);
                }
                else if (string.IsNullOrEmpty(existingSub.Icon) || existingSub.Icon != sub.Icon)
                {
                    existingSub.Icon = sub.Icon;
                    db.SaveChanges();
                }
            }
        }
    }
}
