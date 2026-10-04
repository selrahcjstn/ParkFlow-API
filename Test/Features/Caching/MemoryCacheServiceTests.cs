using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ParkFlow.Application.Common;
using ParkFlow.Infrastructure.Caching;
using Xunit;

namespace Test.Features.Caching;

public class MemoryCacheServiceTests
{
    private MemoryCacheService CreateService()
    {
        var memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return new MemoryCacheService(memoryCache);
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ShouldReturnNull()
    {
        var service = CreateService();

        var result = await service.GetAsync<string>("nonexistent-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_ShouldStoreAndRetrieveValue()
    {
        var service = CreateService();
        var key = "test:user:123";
        var value = "John Doe";

        await service.SetAsync(key, value, TimeSpan.FromMinutes(1));
        var result = await service.GetAsync<string>(key);

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task RemoveAsync_ShouldEvictItemFromCache()
    {
        var service = CreateService();
        var key = "test:item:456";
        await service.SetAsync(key, 999, TimeSpan.FromMinutes(1));

        var retrievedBefore = await service.GetAsync<int?>(key);
        Assert.Equal(999, retrievedBefore);

        await service.RemoveAsync(key);
        var retrievedAfter = await service.GetAsync<int?>(key);
        Assert.Null(retrievedAfter);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_ShouldEvictAllMatchingKeys()
    {
        var service = CreateService();
        var prefix = "dashboard:";

        await service.SetAsync($"{prefix}summary:cap_100", "summary_data", TimeSpan.FromMinutes(1));
        await service.SetAsync($"{prefix}summary:cap_200", "summary_data_2", TimeSpan.FromMinutes(1));
        await service.SetAsync("sessions:count", "session_count_data", TimeSpan.FromMinutes(1));

        await service.RemoveByPrefixAsync(prefix);

        var item1 = await service.GetAsync<string>($"{prefix}summary:cap_100");
        var item2 = await service.GetAsync<string>($"{prefix}summary:cap_200");
        var otherItem = await service.GetAsync<string>("sessions:count");

        Assert.Null(item1);
        Assert.Null(item2);
        Assert.Equal("session_count_data", otherItem);
    }

    [Fact]
    public async Task GetOrCreateAsync_OnCacheMiss_ShouldExecuteFactoryAndCache()
    {
        var service = CreateService();
        var key = "test:factory:item";
        var factoryExecutionCount = 0;

        Task<string> Factory()
        {
            factoryExecutionCount++;
            return Task.FromResult("generated_value");
        }

        var result1 = await service.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(1));
        var result2 = await service.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(1));

        Assert.Equal("generated_value", result1);
        Assert.Equal("generated_value", result2);
        Assert.Equal(1, factoryExecutionCount);
    }

    [Fact]
    public void CacheKeys_GeneratesExpectedFormats()
    {
        Assert.Equal("dashboard:summary:cap_150", CacheKeys.DashboardSummary(150));
        Assert.Equal("sessions:count:cap_100", CacheKeys.ActiveSessionCount(100));
        Assert.Equal("sessions:active:cap_50", CacheKeys.ActiveSessions(50));
        Assert.Equal("announcements:active", CacheKeys.ActiveAnnouncement);

        var userId = Guid.NewGuid();
        Assert.Equal($"user:profile:{userId}", CacheKeys.UserProfile(userId));
        Assert.Equal($"vehicles:owner:{userId}", CacheKeys.UserVehicles(userId));
        Assert.Equal($"schedules:user:{userId}", CacheKeys.UserSchedules(userId));
    }
}
