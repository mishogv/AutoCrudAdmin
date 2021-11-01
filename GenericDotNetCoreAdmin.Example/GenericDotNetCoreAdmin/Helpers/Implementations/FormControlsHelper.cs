namespace GenericDotNetCoreAdmin.Helpers.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using GenericDotNetCoreAdmin.Extensions;
    using GenericDotNetCoreAdmin.ViewModels;
    using Microsoft.EntityFrameworkCore;

    public class FormControlsHelper
        : IFormControlsHelper
    {
        private static readonly ISet<Type> PrimitiveTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(DateTime),
        };

        private readonly DbContext dbContext;

        private static ISet<Type> Types { get; set; }

        static FormControlsHelper()
        {
            Types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(DbContext)))
                .SelectMany(t => t.GetProperties())
                .Where(p => p.PropertyType.IsGenericType
                            && p.PropertyType.Name.StartsWith("DbSet"))
                .Select(p => p.PropertyType)
                .Select(dt => dt.GetGenericArguments().FirstOrDefault())
                .ToHashSet();
        }

        public FormControlsHelper(DbContext dbContext)
            => this.dbContext = dbContext;

        public IEnumerable<FormControlViewModel> GenerateFormControls<TEntity>(TEntity entity)
            => GeneratePrimitiveFormControls(entity)
                .Concat(this.GenerateComplexFormControls(entity));

        private IEnumerable<FormControlViewModel> GenerateComplexFormControls<TEntity>(TEntity entity)
        {
            var entityType = typeof(TEntity);

            var primaryKeyProperty = entityType.GetPrimaryKeyPropertyInfo();
            var result = entityType.GetProperties()
                .Where(property => IsDbContextEntity(property, entity))
                .Select(property => new FormControlViewModel()
                {
                    Name = property.Name,
                    Type = property.PropertyType,
                    Value = this.dbContext.Set(property.PropertyType),
                    IsComplex = true,
                    IsReadOnly = property == primaryKeyProperty,
                })
                .ToList();

            return result;
        }

        private static IEnumerable<FormControlViewModel> GeneratePrimitiveFormControls<TEntity>(TEntity entity)
        {
            var entityType = typeof(TEntity);

            var primaryKeyProperty = entityType.GetPrimaryKeyPropertyInfo();
            return entityType.GetProperties()
                .Where(property => IsPrimitiveProperty(property, entityType))
                .Select(property => new FormControlViewModel
                {
                    Name = property.Name,
                    Type = property.PropertyType,
                    IsReadOnly = property == primaryKeyProperty,
                    Value = property.GetValue(entity),
                    IsComplex = false,
                });
        }

        private static bool IsDbContextEntity<TEntity>(PropertyInfo property, TEntity entity)
            => Types.Contains(property.PropertyType);

        private static bool IsPrimitiveProperty(PropertyInfo property, Type entityType)
            => entityType.GetPrimaryKeyPropertyInfo() == property
               || property.PropertyType.IsEnum
               || (PrimitiveTypes.Contains(property.PropertyType) && !property.Name.ToLower().EndsWith("id"));
    }
}