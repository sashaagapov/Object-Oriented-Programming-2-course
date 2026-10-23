namespace AutoPartsWarehouse.Models;

public static class SeedData
{
    public static void Initialize(WarehouseContext context)
    {
        if (!context.AutoParts.Any())
        {
            var parts = new List<AutoPart>
            {
                new()
                {
                    Name = "Масляний фільтр",
                    Manufacturer = "Bosch",
                    QuantityInStock = 15,
                    MinimumStock = 5
                },
                new()
                {
                    Name = "Гальмівні колодки",
                    Manufacturer = "Bosch",
                    QuantityInStock = 4,
                    MinimumStock = 5
                },
                new()
                {
                    Name = "Повітряний фільтр",
                    Manufacturer = "MANN",
                    QuantityInStock = 12,
                    MinimumStock = 4
                },
                new()
                {
                    Name = "Паливний фільтр",
                    Manufacturer = "MANN",
                    QuantityInStock = 3,
                    MinimumStock = 3
                },
                new()
                {
                    Name = "Гальмівний диск",
                    Manufacturer = "ATE",
                    QuantityInStock = 8,
                    MinimumStock = 4
                },
                new()
                {
                    Name = "Свічка запалювання",
                    Manufacturer = "NGK",
                    QuantityInStock = 24,
                    MinimumStock = 10
                },
                new()
                {
                    Name = "Амортизатор",
                    Manufacturer = "Sachs",
                    QuantityInStock = 6,
                    MinimumStock = 3
                },
                new()
                {
                    Name = "Ремінь ГРМ",
                    Manufacturer = "Continental",
                    QuantityInStock = 2,
                    MinimumStock = 4
                },
                new()
                {
                    Name = "Щітки склоочисника",
                    Manufacturer = "Bosch",
                    QuantityInStock = 18,
                    MinimumStock = 6
                },
                new()
                {
                    Name = "Підшипник маточини",
                    Manufacturer = "SKF",
                    QuantityInStock = 7,
                    MinimumStock = 3
                }
            };

            context.AutoParts.AddRange(parts);
            context.SaveChanges();
        }

        if (!context.Suppliers.Any())
        {
            var suppliers = new List<Supplier>
            {
                new()
                {
                    Name = "AutoTech Supply",
                    Phone = "+380671112233"
                },
                new()
                {
                    Name = "Parts Group",
                    Phone = "+380672223344"
                },
                new()
                {
                    Name = "EuroParts",
                    Phone = "+380673334455"
                }
            };

            context.Suppliers.AddRange(suppliers);
            context.SaveChanges();
        }

        if (!context.PurchaseOrders.Any())
        {
            var supplier1 = context.Suppliers
                .First(supplier => supplier.Name == "AutoTech Supply");

            var supplier2 = context.Suppliers
                .First(supplier => supplier.Name == "EuroParts");

            var oilFilter = context.AutoParts
                .First(part => part.Name == "Масляний фільтр");

            var brakePads = context.AutoParts
                .First(part => part.Name == "Гальмівні колодки");

            var timingBelt = context.AutoParts
                .First(part => part.Name == "Ремінь ГРМ");

            var orders = new List<PurchaseOrder>
            {
                new()
                {
                    Supplier = supplier1,
                    OrderDate = DateTime.Now.AddDays(-10),
                    Status = PurchaseOrderStatus.Received,
                    Items = new List<PurchaseOrderItem>
                    {
                        new()
                        {
                            AutoPart = oilFilter,
                            Quantity = 10
                        },
                        new()
                        {
                            AutoPart = brakePads,
                            Quantity = 5
                        }
                    }
                },

                new()
                {
                    Supplier = supplier2,
                    OrderDate = DateTime.Now.AddDays(-3),
                    Status = PurchaseOrderStatus.Created,
                    Items = new List<PurchaseOrderItem>
                    {
                        new()
                        {
                            AutoPart = timingBelt,
                            Quantity = 8
                        }
                    }
                }
            };

            context.PurchaseOrders.AddRange(orders);
            context.SaveChanges();
        }
        if (!context.VehicleModels.Any())
        {
            var oilFilter = context.AutoParts
                .First(part => part.Name == "Масляний фільтр");

            var brakePads = context.AutoParts
                .First(part => part.Name == "Гальмівні колодки");

            var timingBelt = context.AutoParts
                .First(part => part.Name == "Ремінь ГРМ");

            var vehicles = new List<VehicleModel>
    {
        new()
        {
            Brand = "Volkswagen",
            ModelName = "Golf",
            ProductionYear = 2018,
            AutoParts = new List<AutoPart>
            {
                oilFilter,
                brakePads
            }
        },
        new()
        {
            Brand = "Skoda",
            ModelName = "Octavia",
            ProductionYear = 2020,
            AutoParts = new List<AutoPart>
            {
                oilFilter,
                timingBelt
            }
        }
    };

            context.VehicleModels.AddRange(vehicles);
            context.SaveChanges();
        }
        if (!context.PartIssues.Any())
        {
            var oilFilter = context.AutoParts
                .First(part => part.Name == "Масляний фільтр");

            var brakePads = context.AutoParts
                .First(part => part.Name == "Гальмівні колодки");

            var sparkPlug = context.AutoParts
                .First(part => part.Name == "Свічка запалювання");

            var wipers = context.AutoParts
                .First(part => part.Name == "Щітки склоочисника");

            var issues = new List<PartIssue>
    {
        new()
        {
            AutoPart = oilFilter,
            Quantity = 2,
            IssuedAt = DateTime.Now.AddDays(-20)
        },
        new()
        {
            AutoPart = brakePads,
            Quantity = 1,
            IssuedAt = DateTime.Now.AddDays(-15)
        },
        new()
        {
            AutoPart = sparkPlug,
            Quantity = 4,
            IssuedAt = DateTime.Now.AddDays(-10)
        },
        new()
        {
            AutoPart = oilFilter,
            Quantity = 3,
            IssuedAt = DateTime.Now.AddDays(-7)
        },
        new()
        {
            AutoPart = wipers,
            Quantity = 2,
            IssuedAt = DateTime.Now.AddDays(-3)
        },
        new()
        {
            AutoPart = sparkPlug,
            Quantity = 2,
            IssuedAt = DateTime.Now.AddDays(-1)
        }
    };

            context.PartIssues.AddRange(issues);
            context.SaveChanges();
        }
    }
}