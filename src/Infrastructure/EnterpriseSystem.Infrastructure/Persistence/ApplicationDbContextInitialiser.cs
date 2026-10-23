using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using EnterpriseSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseSystem.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.MigrateAsync();

                await _context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS purchase_orders (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""OrderNumber"" character varying(50) NOT NULL UNIQUE,
                        ""UserId"" uuid NOT NULL,
                        ""UserEmail"" character varying(200) NOT NULL,
                        ""UserFullName"" character varying(200) NOT NULL,
                        ""DeliveryPoint"" character varying(500) NOT NULL,
                        ""PaymentMethod"" character varying(50) NOT NULL,
                        ""Notes"" character varying(1000),
                        ""Status"" character varying(30) NOT NULL,
                        ""TotalAmount"" numeric(18,2) NOT NULL,
                        ""ReviewedAt"" timestamp with time zone,
                        ""ReviewedBy"" character varying(150),
                        ""AdminComments"" character varying(1000),
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""CreatedBy"" character varying(150),
                        ""LastModifiedAt"" timestamp with time zone,
                        ""LastModifiedBy"" character varying(150)
                    );

                    CREATE TABLE IF NOT EXISTS purchase_order_items (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""PurchaseOrderId"" uuid NOT NULL REFERENCES purchase_orders(""Id"") ON DELETE CASCADE,
                        ""ProductId"" uuid NOT NULL REFERENCES products(""Id"") ON DELETE RESTRICT,
                        ""ProductSku"" character varying(50) NOT NULL,
                        ""ProductName"" character varying(200) NOT NULL,
                        ""UnitPrice"" numeric(18,2) NOT NULL,
                        ""Quantity"" integer NOT NULL,
                        ""LineTotal"" numeric(18,2) NOT NULL
                    );
                ");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // 1. Seed Roles
        foreach (var roleName in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }

        // 2. Seed Default Administrator
        var adminEmail = "admin@enterprise.com";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var createResult = await _userManager.CreateAsync(adminUser, "Admin@123456");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, Roles.Administrator);
            }
        }

        // 3. Seed Default Auditor
        var auditorEmail = "auditor@enterprise.com";
        var auditorUser = await _userManager.FindByEmailAsync(auditorEmail);
        if (auditorUser == null)
        {
            auditorUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = auditorEmail,
                Email = auditorEmail,
                FullName = "Compliance Auditor",
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var createResult = await _userManager.CreateAsync(auditorUser, "Auditor@123456");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(auditorUser, Roles.Auditor);
            }
        }

        // 4. Seed Standard User
        var standardEmail = "user@enterprise.com";
        var standardUser = await _userManager.FindByEmailAsync(standardEmail);
        if (standardUser == null)
        {
            standardUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = standardEmail,
                Email = standardEmail,
                FullName = "Standard Operator",
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var createResult = await _userManager.CreateAsync(standardUser, "User@123456");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(standardUser, Roles.StandardUser);
            }
        }

        // 5. Seed Categories & Products if empty
        if (!await _context.Categories.AnyAsync())
        {
            var hardwareCategory = new Category
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Hardware & Servers",
                Description = "Enterprise datacenter servers, racks, and workstations",
                CreatedBy = "SystemInitialiser"
            };

            var networkingCategory = new Category
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Networking Equipment",
                Description = "Core switches, edge routers, transceivers, and patch panels",
                CreatedBy = "SystemInitialiser"
            };

            var officeCategory = new Category
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Office IT Peripherals",
                Description = "Laptops, monitors, docking stations, and ergonomic accessories",
                CreatedBy = "SystemInitialiser"
            };

            var toolsCategory = new Category
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Industrial Tools",
                Description = "Fiber splicers, cable analyzers, OTDR testers, and toolkit sets",
                CreatedBy = "SystemInitialiser"
            };

            _context.Categories.AddRange(hardwareCategory, networkingCategory, officeCategory, toolsCategory);

            var products = new List<Product>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "SRV-DL380-G10",
                    Name = "HPE ProLiant DL380 Gen10 Server",
                    Description = "2x Intel Xeon Gold 6248R 24-Core, 256GB RAM, 8x 1.92TB NVMe SSD, redundant 800W PSU",
                    Price = 7450.00m,
                    StockQuantity = 14,
                    Status = ProductStatus.Active,
                    CategoryId = hardwareCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "SRV-R750-XS",
                    Name = "Dell EMC PowerEdge R750xs",
                    Description = "Dual Intel Xeon Silver 4314 16C, 128GB DDR4, PERC H755 RAID, dual 10GbE SFP+",
                    Price = 5290.00m,
                    StockQuantity = 8,
                    Status = ProductStatus.Active,
                    CategoryId = hardwareCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "NET-CAT9300-48P",
                    Name = "Cisco Catalyst 9300 48-Port PoE+",
                    Description = "Enterprise-grade layer 3 managed switch with 48x Gigabit PoE+ ports and modular uplink slot",
                    Price = 3890.00m,
                    StockQuantity = 22,
                    Status = ProductStatus.Active,
                    CategoryId = networkingCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "NET-FORTI-100F",
                    Name = "Fortinet FortiGate 100F Next-Gen Firewall",
                    Description = "1 Gbps threat protection throughput, 22x GE RJ45, 4x 10GE SFP+ slots, redundant power supply",
                    Price = 2850.00m,
                    StockQuantity = 5,
                    Status = ProductStatus.Active,
                    CategoryId = networkingCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "LAP-THINK-P16",
                    Name = "Lenovo ThinkPad P16s Gen 2",
                    Description = "16-inch WQXGA, Intel Core i7-1370P, 32GB LPDDR5, 1TB PCIe 4.0 NVMe, NVIDIA RTX A500",
                    Price = 1899.99m,
                    StockQuantity = 45,
                    Status = ProductStatus.Active,
                    CategoryId = officeCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "MON-DELL-U2723QE",
                    Name = "Dell UltraSharp 27 4K USB-C Hub Monitor",
                    Description = "27-inch 4K UHD IPS Black, 98% DCI-P3, 90W Power Delivery, RJ45 Ethernet, Daisy Chain",
                    Price = 579.50m,
                    StockQuantity = 60,
                    Status = ProductStatus.Active,
                    CategoryId = officeCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "TOOL-FLUKE-DSX8000",
                    Name = "Fluke Networks DSX-8000 CableAnalyzer",
                    Description = "Category 8 copper tester certified to ISO Level 2G / TIA Level 2G accuracy, 2 GHz frequency range",
                    Price = 14200.00m,
                    StockQuantity = 2,
                    Status = ProductStatus.Active,
                    CategoryId = toolsCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "TOOL-FIBER-FSM90S",
                    Name = "Fujikura 90S+ Core Alignment Fusion Splicer",
                    Description = "Active Fusion Control Technology, Automated wind protector, sheath clamps, 5-second splice time",
                    Price = 8900.00m,
                    StockQuantity = 0,
                    Status = ProductStatus.OutOfStock,
                    CategoryId = toolsCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "SRV-BLADE-LEGACY",
                    Name = "IBM BladeCenter HS22 Legacy Server",
                    Description = "Decommissioned 2-socket blade server retained for spare parts and retro inventory auditing",
                    Price = 450.00m,
                    StockQuantity = 3,
                    Status = ProductStatus.Discontinued,
                    CategoryId = hardwareCategory.Id,
                    CreatedBy = "SystemInitialiser"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Sku = "NET-WIFI7-AP",
                    Name = "Aruba AP-735 Wi-Fi 7 Enterprise Access Point",
                    Description = "Tri-band 802.11be enterprise AP with dual 5GbE Smart Rate Ethernet ports (Engineering sample)",
                    Price = 1150.00m,
                    StockQuantity = 10,
                    Status = ProductStatus.Draft,
                    CategoryId = networkingCategory.Id,
                    CreatedBy = "SystemInitialiser"
                }
            };

            _context.Products.AddRange(products);
            await _context.SaveChangesAsync();
        }
    }
}
