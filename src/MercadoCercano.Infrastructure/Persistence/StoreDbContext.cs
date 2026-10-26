using MercadoCercano.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Infrastructure.Persistence;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionUsage> PromotionUsages => Set<PromotionUsage>();
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<PaymentEvidence> PaymentEvidence => Set<PaymentEvidence>();
    public DbSet<OrderReturn> OrderReturns => Set<OrderReturn>();
    public DbSet<OrderReturnItem> OrderReturnItems => Set<OrderReturnItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", table =>
                table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN (1, 2)"));
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(160).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.UpdatedAtUtc).IsConcurrencyToken();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.HasIndex(user => user.IsActive);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.FullName).HasMaxLength(160).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(254);
            entity.Property(customer => customer.Phone).HasMaxLength(32).IsRequired();
            entity.Property(customer => customer.BusinessNotes).HasMaxLength(2000);
            entity.HasIndex(customer => customer.Phone);
            entity.HasIndex(customer => customer.Email);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Addresses");
            entity.HasKey(address => address.Id);
            entity.Property(address => address.Municipality).HasMaxLength(120).IsRequired();
            entity.Property(address => address.Neighborhood).HasMaxLength(120);
            entity.Property(address => address.AddressLine).HasMaxLength(300).IsRequired();
            entity.Property(address => address.References).HasMaxLength(500);
            entity.HasIndex(address => new { address.CustomerId, address.IsDefault });
            entity.HasOne(address => address.Customer)
                .WithMany(customer => customer.Addresses)
                .HasForeignKey(address => address.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories", table =>
                table.HasCheckConstraint("CK_Categories_SortOrder", "\"SortOrder\" >= 0"));
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.Property(category => category.Slug).HasMaxLength(140).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(2000);
            entity.Property(category => category.ImageUrl).HasMaxLength(2048);
            entity.HasIndex(category => category.Slug).IsUnique();
            entity.HasIndex(category => new { category.IsActive, category.SortOrder });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", table =>
            {
                table.HasCheckConstraint("CK_Products_Price", "\"Price\" > 0");
                table.HasCheckConstraint("CK_Products_Cost", "\"Cost\" >= 0");
                table.HasCheckConstraint(
                    "CK_Products_PromotionalPrice",
                    "\"PromotionalPrice\" IS NULL OR (\"PromotionalPrice\" > 0 AND \"PromotionalPrice\" < \"Price\")");
                table.HasCheckConstraint(
                    "CK_Products_PromotionDates",
                    "(\"PromotionStartUtc\" IS NULL AND \"PromotionEndUtc\" IS NULL) OR (\"PromotionStartUtc\" IS NOT NULL AND \"PromotionEndUtc\" > \"PromotionStartUtc\")");
                table.HasCheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0");
                table.HasCheckConstraint("CK_Products_LowStockThreshold", "\"LowStockThreshold\" >= 0");
                table.HasCheckConstraint("CK_Products_WeightGrams", "\"WeightGrams\" IS NULL OR \"WeightGrams\" > 0");
            });
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Sku).HasMaxLength(80).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(180).IsRequired();
            entity.Property(product => product.Slug).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(10000).IsRequired();
            entity.Property(product => product.Unit).HasMaxLength(40).IsRequired();
            entity.Property(product => product.Presentation).HasMaxLength(300);
            entity.Property(product => product.LotNumber).HasMaxLength(120);
            entity.Property(product => product.SupplierLabelingJson).HasMaxLength(10000);
            entity.HasIndex(product => product.Sku).IsUnique();
            entity.HasIndex(product => product.Slug).IsUnique();
            entity.HasIndex(product => new { product.IsActive, product.CategoryId });
            entity.HasIndex(product => new { product.IsActive, product.IsFeatured });
            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages", table =>
                table.HasCheckConstraint("CK_ProductImages_SortOrder", "\"SortOrder\" >= 0"));
            entity.HasKey(image => image.Id);
            entity.Property(image => image.ImageUrl).HasMaxLength(2048).IsRequired();
            entity.Property(image => image.AltText).HasMaxLength(300);
            entity.HasIndex(image => new { image.ProductId, image.SortOrder });
            entity.HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table =>
            {
                table.HasCheckConstraint("CK_Orders_Subtotal", "\"Subtotal\" >= 0");
                table.HasCheckConstraint(
                    "CK_Orders_DiscountTotal",
                    "\"DiscountTotal\" >= 0 AND \"DiscountTotal\" <= \"Subtotal\"");
                table.HasCheckConstraint("CK_Orders_ShippingCost", "\"ShippingCost\" >= 0");
                table.HasCheckConstraint(
                    "CK_Orders_GrandTotal",
                    "\"GrandTotal\" = \"Subtotal\" - \"DiscountTotal\" + \"ShippingCost\"");
                table.HasCheckConstraint("CK_Orders_DeliveryMethod", "\"DeliveryMethod\" IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Orders_PaymentMethod", "\"PaymentMethod\" IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Orders_Status", "\"Status\" BETWEEN 1 AND 7");
                table.HasCheckConstraint("CK_Orders_PaymentStatus", "\"PaymentStatus\" BETWEEN 1 AND 5");
            });
            entity.HasKey(order => order.Id);
            entity.Property(order => order.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(order => order.CustomerNameSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(order => order.CustomerPhoneSnapshot).HasMaxLength(32).IsRequired();
            entity.Property(order => order.CustomerEmailSnapshot).HasMaxLength(254);
            entity.Property(order => order.MunicipalitySnapshot).HasMaxLength(120).IsRequired();
            entity.Property(order => order.NeighborhoodSnapshot).HasMaxLength(120);
            entity.Property(order => order.AddressSnapshot).HasMaxLength(300);
            entity.Property(order => order.AddressReferencesSnapshot).HasMaxLength(500);
            entity.Property(order => order.PromotionCodeSnapshot).HasMaxLength(80);
            entity.Property(order => order.PromotionNameSnapshot).HasMaxLength(160);
            entity.Property(order => order.CustomerNotes).HasMaxLength(2000);
            entity.Property(order => order.CancellationReason).HasMaxLength(1000);
            entity.Property(order => order.TrackingTokenHash).HasMaxLength(128);
            entity.Property(order => order.IdempotencyKey).HasMaxLength(128);
            entity.Property(order => order.RequestFingerprint).HasMaxLength(64);
            entity.Property(order => order.CheckoutNoticeSnapshot).HasMaxLength(5000);
            entity.Property(order => order.PickupInstructionsSnapshot).HasMaxLength(1000);
            entity.Property(order => order.PaymentInstructionsSnapshot).HasMaxLength(1000);
            entity.HasIndex(order => order.OrderNumber).IsUnique();
            entity.HasIndex(order => order.TrackingTokenHash).IsUnique();
            entity.HasIndex(order => order.IdempotencyKey).IsUnique();
            entity.HasIndex(order => new { order.Status, order.CreatedAtUtc });
            entity.HasIndex(order => new { order.PaymentStatus, order.CreatedAtUtc });
            entity.HasIndex(order => order.CustomerPhoneSnapshot);
            entity.HasOne(order => order.Customer)
                .WithMany()
                .HasForeignKey(order => order.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(order => order.CancelledByUser)
                .WithMany()
                .HasForeignKey(order => order.CancelledByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems", table =>
            {
                table.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" > 0");
                table.HasCheckConstraint("CK_OrderItems_UnitCostSnapshot", "\"UnitCostSnapshot\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_LineTotal", "\"LineTotal\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_PromotionDiscount", "\"PromotionDiscountTotal\" BETWEEN 0 AND \"LineTotal\"");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductNameSnapshot).HasMaxLength(180).IsRequired();
            entity.Property(item => item.SkuSnapshot).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.OrderId);
            entity.HasOne(item => item.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0");
                table.HasCheckConstraint("CK_Payments_Method", "\"Method\" IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Payments_Status", "\"Status\" BETWEEN 1 AND 5");
            });
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Reference).HasMaxLength(160);
            entity.Property(payment => payment.RejectionReason).HasMaxLength(1000);
            entity.HasIndex(payment => new { payment.OrderId, payment.CreatedAtUtc });
            entity.HasOne(payment => payment.Order)
                .WithMany(order => order.Payments)
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(payment => payment.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements", table =>
            {
                table.HasCheckConstraint("CK_InventoryMovements_Type", "\"MovementType\" BETWEEN 1 AND 6");
                table.HasCheckConstraint("CK_InventoryMovements_QuantityDelta", "\"QuantityDelta\" <> 0");
                table.HasCheckConstraint("CK_InventoryMovements_PreviousQuantity", "\"PreviousQuantity\" >= 0");
                table.HasCheckConstraint("CK_InventoryMovements_NewQuantity", "\"NewQuantity\" >= 0");
                table.HasCheckConstraint(
                    "CK_InventoryMovements_QuantityReconciliation",
                    "\"NewQuantity\" = \"PreviousQuantity\" + \"QuantityDelta\"");
            });
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(movement => movement.ReferenceType).HasMaxLength(80);
            entity.Property(movement => movement.ReferenceId).HasMaxLength(128);
            entity.HasIndex(movement => new { movement.ProductId, movement.CreatedAtUtc });
            entity.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId });
            entity.HasOne(movement => movement.Product)
                .WithMany()
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movement => movement.User)
                .WithMany()
                .HasForeignKey(movement => movement.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Order>().Property(order => order.CarrierName).HasMaxLength(120);
        modelBuilder.Entity<Order>().Property(order => order.TrackingNumber).HasMaxLength(120);
        modelBuilder.Entity<PaymentEvidence>(entity =>
        {
            entity.ToTable("PaymentEvidence", table => table.HasCheckConstraint("CK_PaymentEvidence_Length", "\"ByteLength\" > 0 AND \"ByteLength\" <= 768000"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.StoredFileName).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.StoredFileName).IsUnique();
            entity.HasOne(item => item.Payment).WithMany(payment => payment.Evidence)
                .HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OrderReturn>(entity =>
        {
            entity.ToTable("OrderReturns", table =>
            {
                table.HasCheckConstraint("CK_OrderReturns_Kind", "\"Kind\" BETWEEN 1 AND 3");
                table.HasCheckConstraint("CK_OrderReturns_Status", "\"Status\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_OrderReturns_Amount", "\"RefundAmount\" BETWEEN 0 AND 9000000000000 AND \"ShippingRefundAmount\" BETWEEN 0 AND \"RefundAmount\"");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.RequestId).IsUnique();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64);
            entity.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.RejectionReason).HasMaxLength(1000);
            entity.Property(item => item.RefundReference).HasMaxLength(160);
            entity.HasOne(item => item.Order).WithMany(order => order.Returns)
                .HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ReceivedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RefundedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<OrderReturnItem>(entity =>
        {
            entity.ToTable("OrderReturnItems", table =>
            {
                table.HasCheckConstraint("CK_OrderReturnItems_Quantity", "\"Quantity\" > 0 AND \"RestockedQuantity\" BETWEEN 0 AND \"Quantity\"");
                table.HasCheckConstraint("CK_OrderReturnItems_Amount", "\"RefundAmount\" BETWEEN 0 AND 9000000000000");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.OrderReturnId, item.OrderItemId }).IsUnique();
            entity.HasOne(item => item.Return).WithMany(item => item.Items).HasForeignKey(item => item.OrderReturnId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OrderItem).WithMany().HasForeignKey(item => item.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.ToTable("Promotions", table =>
            {
                table.HasCheckConstraint("CK_Promotions_DiscountType", "\"DiscountType\" IN (1, 2)");
                table.HasCheckConstraint("CK_Promotions_DiscountValue", "\"DiscountValue\" > 0");
                table.HasCheckConstraint(
                    "CK_Promotions_Percentage",
                    "\"DiscountType\" <> 1 OR \"DiscountValue\" <= 100");
                table.HasCheckConstraint("CK_Promotions_MinimumOrderAmount", "\"MinimumOrderAmount\" >= 0");
                table.HasCheckConstraint("CK_Promotions_MaxUses", "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
                table.HasCheckConstraint(
                    "CK_Promotions_UsageCount",
                    "\"UsedCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"UsedCount\" <= \"MaxUses\")");
                table.HasCheckConstraint("CK_Promotions_DateRange", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.HasCheckConstraint("CK_Promotions_Scope", "\"ProductId\" IS NULL OR \"CategoryId\" IS NULL");
                table.HasCheckConstraint("CK_Promotions_MaximumDiscount", "\"MaximumDiscountAmount\" IS NULL OR \"MaximumDiscountAmount\" BETWEEN 1 AND 9000000000000");
            });
            entity.HasKey(promotion => promotion.Id);
            entity.Property(promotion => promotion.Name).HasMaxLength(160).IsRequired();
            entity.Property(promotion => promotion.Code).HasMaxLength(80);
            entity.HasOne(promotion => promotion.Product).WithMany().HasForeignKey(promotion => promotion.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(promotion => promotion.Category).WithMany().HasForeignKey(promotion => promotion.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(promotion => promotion.Code).IsUnique();
            entity.HasIndex(promotion => new { promotion.IsActive, promotion.StartsAtUtc, promotion.EndsAtUtc });
        });

        modelBuilder.Entity<PromotionUsage>(entity =>
        {
            entity.ToTable("PromotionUsages", table =>
                table.HasCheckConstraint("CK_PromotionUsages_DiscountAmount", "\"DiscountAmount\" BETWEEN 0 AND 9000000000000"));
            entity.HasKey(usage => usage.Id);
            entity.HasIndex(usage => new { usage.PromotionId, usage.OrderId }).IsUnique();
            entity.HasIndex(usage => usage.OrderId).IsUnique();
            entity.HasOne(usage => usage.Promotion)
                .WithMany(promotion => promotion.Usages)
                .HasForeignKey(usage => usage.PromotionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(usage => usage.Order)
                .WithMany()
                .HasForeignKey(usage => usage.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoreSetting>(entity =>
        {
            entity.ToTable("StoreSettings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key).HasMaxLength(120).IsRequired();
            entity.Property(setting => setting.Value).HasMaxLength(10000).IsRequired();
            entity.HasIndex(setting => setting.Key).IsUnique();
            entity.HasIndex(setting => setting.IsPublic);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(120).IsRequired();
            entity.Property(log => log.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(128);
            entity.Property(log => log.DetailsJson).HasMaxLength(10000);
            entity.HasIndex(log => log.CreatedAtUtc);
            entity.HasIndex(log => new { log.EntityType, log.EntityId });
            entity.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("OrderStatusHistory", table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderStatusHistory_PreviousStatus",
                    "\"PreviousStatus\" IS NULL OR \"PreviousStatus\" BETWEEN 1 AND 7");
                table.HasCheckConstraint("CK_OrderStatusHistory_NewStatus", "\"NewStatus\" BETWEEN 1 AND 7");
            });
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Reason).HasMaxLength(1000).IsRequired();
            entity.HasIndex(history => new { history.OrderId, history.CreatedAtUtc });
            entity.HasOne(history => history.Order)
                .WithMany(order => order.StatusHistory)
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(history => history.User)
                .WithMany()
                .HasForeignKey(history => history.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
