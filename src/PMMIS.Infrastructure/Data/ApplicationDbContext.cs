using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;

namespace PMMIS.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Auto-convert all DateTime properties to UTC before saving (PostgreSQL requirement)
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        FixDateTimeKinds();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        FixDateTimeKinds();
        return base.SaveChanges();
    }

    private void FixDateTimeKinds()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                    {
                        prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }
                }
            }
        }
    }

    // Project hierarchy
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<SubComponent> SubComponents => Set<SubComponent>();
    
    // Contracts and Contractors
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WorkProgress> WorkProgresses => Set<WorkProgress>();
    
    // Geography
    public DbSet<District> Districts => Set<District>();
    public DbSet<Jamoat> Jamoats => Set<Jamoat>();
    public DbSet<Village> Villages => Set<Village>();
    
    // Facilities
    public DbSet<School> Schools => Set<School>();
    public DbSet<HealthFacility> HealthFacilities => Set<HealthFacility>();
    
    // Reference Data
    public DbSet<EducationInstitutionType> EducationInstitutionTypes => Set<EducationInstitutionType>();
    public DbSet<HealthFacilityType> HealthFacilityTypes => Set<HealthFacilityType>();
    public DbSet<IndicatorCategory> IndicatorCategories => Set<IndicatorCategory>();
    
    // Indicators
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<IndicatorValue> IndicatorValues => Set<IndicatorValue>();
    
    // Budget
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<BudgetExpense> BudgetExpenses => Set<BudgetExpense>();
    
    // Documents and Notifications
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Notification> Notifications => Set<Notification>();
    
    // Role Permissions
    public DbSet<RoleMenuPermission> RoleMenuPermissions => Set<RoleMenuPermission>();
    
    // Procurement Plan
    public DbSet<ProcurementPlan> ProcurementPlans => Set<ProcurementPlan>();
    
    // User Settings
    public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
    
    // Tasks
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskExtensionRequest> TaskExtensionRequests => Set<TaskExtensionRequest>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<TaskChecklist> TaskChecklists => Set<TaskChecklist>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    
    // Contract Indicators
    public DbSet<ContractIndicator> ContractIndicators => Set<ContractIndicator>();
    public DbSet<ContractIndicatorProgress> ContractIndicatorProgresses => Set<ContractIndicatorProgress>();
    public DbSet<ContractIndicatorVillage> ContractIndicatorVillages => Set<ContractIndicatorVillage>();
    public DbSet<IndicatorProgressItem> IndicatorProgressItems => Set<IndicatorProgressItem>();
    
    // Contract Milestones
    public DbSet<ContractMilestone> ContractMilestones => Set<ContractMilestone>();

    // Currency Rate Cache
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // User hierarchy (self-referencing FK)
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Supervisor)
                .WithMany(u => u.Subordinates)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Project hierarchy
        builder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.TotalBudget).HasPrecision(18, 2);
        });

        builder.Entity<Component>(e =>
        {
            e.HasOne(c => c.Project)
                .WithMany(p => p.Components)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.AllocatedBudget).HasPrecision(18, 2);
        });

        builder.Entity<SubComponent>(e =>
        {
            e.HasOne(sc => sc.Component)
                .WithMany(c => c.SubComponents)
                .HasForeignKey(sc => sc.ComponentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(sc => sc.AllocatedBudget).HasPrecision(18, 2);
        });

        // Contracts
        builder.Entity<Contract>(e =>
        {
            e.HasIndex(c => c.ContractNumber).IsUnique();
            e.Property(c => c.ContractAmount).HasPrecision(18, 2);
            e.Property(c => c.AdditionalAmount).HasPrecision(18, 2);
            e.Property(c => c.SavedAmount).HasPrecision(18, 2);
            e.Property(c => c.WorkCompletedPercent).HasPrecision(5, 2);
            
            e.HasOne(c => c.Project)
                .WithMany(p => p.Contracts)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.HasOne(c => c.SubComponent)
                .WithMany(sc => sc.Contracts)
                .HasForeignKey(c => c.SubComponentId)
                .OnDelete(DeleteBehavior.SetNull);
                
            e.HasOne(c => c.Contractor)
                .WithMany(ct => ct.Contracts)
                .HasForeignKey(c => c.ContractorId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Contract ↔ ProcurementPlan (one-to-one, Contract is dependent)
            e.HasOne(c => c.ProcurementPlan)
                .WithOne()
                .HasForeignKey<Contract>(c => c.ProcurementPlanId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Responsible persons
            e.HasOne(c => c.Curator)
                .WithMany()
                .HasForeignKey(c => c.CuratorId)
                .OnDelete(DeleteBehavior.SetNull);
                
            e.HasOne(c => c.ProjectManager)
                .WithMany()
                .HasForeignKey(c => c.ProjectManagerId)
                .OnDelete(DeleteBehavior.SetNull);
                
            e.Ignore(c => c.FinalAmount);
            e.Ignore(c => c.PaidAmount);
            e.Ignore(c => c.PaidPercent);
            e.Ignore(c => c.RemainingAmount);
            e.Ignore(c => c.RemainingDays);
        });

        // Payments
        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.HasOne(p => p.Contract)
                .WithMany(c => c.Payments)
                .HasForeignKey(p => p.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.WorkProgress)
                .WithMany()
                .HasForeignKey(p => p.WorkProgressId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // WorkProgress
        builder.Entity<WorkProgress>(e =>
        {
            e.Property(wp => wp.CompletedPercent).HasPrecision(5, 2);
            e.HasOne(wp => wp.Contract)
                .WithMany(c => c.WorkProgresses)
                .HasForeignKey(wp => wp.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Approval workflow FKs
            e.HasOne(wp => wp.ManagerReviewedBy)
                .WithMany()
                .HasForeignKey(wp => wp.ManagerReviewedById)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(wp => wp.DirectorApprovedBy)
                .WithMany()
                .HasForeignKey(wp => wp.DirectorApprovedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Geography
        builder.Entity<District>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();
        });

        builder.Entity<Jamoat>(e =>
        {
            e.HasOne(j => j.District)
                .WithMany(d => d.Jamoats)
                .HasForeignKey(j => j.DistrictId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Village>(e =>
        {
            e.HasOne(v => v.Jamoat)
                .WithMany(j => j.Villages)
                .HasForeignKey(v => v.JamoatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Indicators
        builder.Entity<Indicator>(e =>
        {
            e.HasIndex(i => i.Code).IsUnique();
            e.Property(i => i.TargetValue).HasPrecision(18, 2);
        });

        builder.Entity<IndicatorValue>(e =>
        {
            e.Property(iv => iv.Value).HasPrecision(18, 2);
            e.HasOne(iv => iv.Indicator)
                .WithMany(i => i.Values)
                .HasForeignKey(iv => iv.IndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Budget
        builder.Entity<BudgetItem>(e =>
        {
            e.Property(bi => bi.AllocatedAmount).HasPrecision(18, 2);
            e.Ignore(bi => bi.SpentAmount);
            e.Ignore(bi => bi.RemainingAmount);
        });

        builder.Entity<BudgetExpense>(e =>
        {
            e.Property(be => be.Amount).HasPrecision(18, 2);
            e.HasOne(be => be.BudgetItem)
                .WithMany(bi => bi.Expenses)
                .HasForeignKey(be => be.BudgetItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Documents
        builder.Entity<Document>(e =>
        {
            e.HasOne(d => d.Contract)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.SetNull);
                
            e.HasOne(d => d.WorkProgress)
                .WithMany(wp => wp.Documents)
                .HasForeignKey(d => d.WorkProgressId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // One-to-one with Payment (Document is dependent)
            e.HasOne(d => d.Payment)
                .WithOne(p => p.Document)
                .HasForeignKey<Document>(d => d.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);
                
            e.HasOne(d => d.Contractor)
                .WithMany(ct => ct.Documents)
                .HasForeignKey(d => d.ContractorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Notifications
        builder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });
        
        // Contract Indicators
        builder.Entity<ContractIndicator>(e =>
        {
            e.Property(ci => ci.TargetValue).HasPrecision(18, 2);
            e.Property(ci => ci.AchievedValue).HasPrecision(18, 2);
            e.Ignore(ci => ci.ProgressPercent);
            
            e.HasOne(ci => ci.Contract)
                .WithMany(c => c.ContractIndicators)
                .HasForeignKey(ci => ci.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasOne(ci => ci.Indicator)
                .WithMany()
                .HasForeignKey(ci => ci.IndicatorId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Unique constraint: one indicator per contract
            e.HasIndex(ci => new { ci.ContractId, ci.IndicatorId }).IsUnique();
        });
        
        // Contract Indicator Villages (junction)
        builder.Entity<ContractIndicatorVillage>(e =>
        {
            e.HasOne(civ => civ.ContractIndicator)
                .WithMany(ci => ci.Villages)
                .HasForeignKey(civ => civ.ContractIndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasOne(civ => civ.Village)
                .WithMany()
                .HasForeignKey(civ => civ.VillageId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Unique constraint: one village per contract indicator
            e.HasIndex(civ => new { civ.ContractIndicatorId, civ.VillageId }).IsUnique();
        });
        
        builder.Entity<ContractIndicatorProgress>(e =>
        {
            e.Property(cip => cip.Value).HasPrecision(18, 2);
            
            e.HasOne(cip => cip.ContractIndicator)
                .WithMany(ci => ci.Progresses)
                .HasForeignKey(cip => cip.ContractIndicatorId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasOne(cip => cip.WorkProgress)
                .WithMany(wp => wp.IndicatorProgresses)
                .HasForeignKey(cip => cip.WorkProgressId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Contract Milestones
        builder.Entity<ContractMilestone>(e =>
        {
            e.HasOne(cm => cm.Contract)
                .WithMany(c => c.Milestones)
                .HasForeignKey(cm => cm.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasOne(cm => cm.WorkProgress)
                .WithMany()
                .HasForeignKey(cm => cm.WorkProgressId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // ProcurementPlan
        builder.Entity<ProcurementPlan>(e =>
        {
            e.Property(pp => pp.EstimatedAmount).HasPrecision(18, 2);
            
            // ProcurementPlan.ContractId → Contract (separate from Contract.ProcurementPlanId)
            e.HasOne(pp => pp.Contract)
                .WithMany()
                .HasForeignKey(pp => pp.ContractId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Currency Rate Cache
        builder.Entity<CurrencyRate>(e =>
        {
            e.HasIndex(cr => new { cr.Date, cr.CharCode }).IsUnique();
            e.Property(cr => cr.Value).HasPrecision(18, 6);
        });
    }
}
