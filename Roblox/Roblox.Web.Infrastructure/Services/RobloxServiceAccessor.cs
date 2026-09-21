using Microsoft.Extensions.DependencyInjection;
using Roblox.Libraries.DiscordApi;
using Roblox.Libraries.LeakCheckApi;
using Roblox.Libraries.RobloxApi;
using Roblox.Services;
using Roblox.Services.AdminApi;
using Roblox.Services.Donations;
using Roblox.Services.Games;
using Roblox.Services.PlaceLauncher;
using Roblox.Services.Signer;

namespace Roblox.Web.Infrastructure.Services;

public class RobloxServiceAccessor : IDisposable
{
    private readonly IServiceProvider? _serviceProvider;

    private AssetsService? _assets;
    private PromocodesService? _promocodes;
    private RobloxAssetService? _robloxAssetCache;
    private UsersService? _users;
    private AccountInformationService? _accountInformation;
    private AvatarService? _avatar;
    private FriendsService? _friends;
    private GamesService? _games;
    private PlayerSecurityService? _playerSecurity;
    private BadgesService? _badges;
    private GroupsService? _groups;
    private InventoryService? _inventory;
    private PrivateMessagesService? _privateMessages;
    private ThumbnailsService? _thumbnails;
    private TradesService? _trades;
    private GameServerService? _gameServer;
    private SetsService? _sets;
    private PlaceLauncherService? _placeLauncher;
    private SignService? _sign;
    private ForumsService? _forums;
    private CurrencyExchangeService? _currencyExchange;
    private AbuseReportService? _abuseReport;
    private EconomyService? _economy;
    private PurchaseAttestationService? _purchaseAttestation;
    private SessionNegotiationTicketService? _sessionNegotiationTickets;
    private CooldownService? _cooldown;
    private FilterService? _filter;
    private ChatService? _chat;
    private AdminApiService? _adminApi;
    private DataStoreService? _dataStore;
    private R2StorageService? _r2Storage;
    private RobloxApi? _robloxApi;
    private LeakCheckApi? _leakCheck;
    private DiscordBotApi? _discordBotApi;
    private DonationRewardService? _donationRewards;
    private AuthenticationService? _authentication;
    private MachineBanService? _machineBan;

    public RobloxServiceAccessor()
    {
    }

    public RobloxServiceAccessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public AssetsService assets => GetService(ref _assets);
    public PromocodesService promocodes => GetService(ref _promocodes);
    public RobloxAssetService robloxAssetCache => GetService(ref _robloxAssetCache);
    public UsersService users => GetService(ref _users);
    public AccountInformationService accountInformation => GetService(ref _accountInformation);
    public AvatarService avatar => GetService(ref _avatar);
    public FriendsService friends => GetService(ref _friends);
    public GamesService games => GetService(ref _games);
    public PlayerSecurityService playerSecurity => GetService(ref _playerSecurity);
    public BadgesService badges => GetService(ref _badges);
    public GroupsService groups => GetService(ref _groups);
    public InventoryService inventory => GetService(ref _inventory);
    public PrivateMessagesService privateMessages => GetService(ref _privateMessages);
    public ThumbnailsService thumbnails => GetService(ref _thumbnails);
    public TradesService trades => GetService(ref _trades);
    public GameServerService gameServer => GetService(ref _gameServer);
    public SetsService sets => GetService(ref _sets);
    public PlaceLauncherService placeLauncher => GetService(ref _placeLauncher);
    public SignService sign => GetService(ref _sign);
    public ForumsService forums => GetService(ref _forums);
    public CurrencyExchangeService currencyExchange => GetService(ref _currencyExchange);
    public AbuseReportService abuseReport => GetService(ref _abuseReport);
    public EconomyService economy => GetService(ref _economy);
    public PurchaseAttestationService purchaseAttestation => GetService(ref _purchaseAttestation);
    public SessionNegotiationTicketService sessionNegotiationTickets => GetService(ref _sessionNegotiationTickets);
    public CooldownService cooldown => GetService(ref _cooldown);
    public FilterService filter => GetService(ref _filter);
    public ChatService chat => GetService(ref _chat);
    public AdminApiService adminApi => GetService(ref _adminApi);
    public DataStoreService dataStore => GetService(ref _dataStore);
    public R2StorageService r2Storage => GetService(ref _r2Storage);
    public DonationRewardService donationRewards => GetService(ref _donationRewards);
    public AuthenticationService authentication => GetService(ref _authentication);
    public MachineBanService machineBan => GetService(ref _machineBan);
    public RobloxApi robloxApi => _robloxApi ??= new RobloxApi();
    public LeakCheckApi leakCheck => _leakCheck ??= new LeakCheckApi(Roblox.Configuration.LeakCheckApiKey);
    public DiscordBotApi discordBotApi => _discordBotApi ??= new DiscordBotApi(Roblox.Configuration.DiscordBotToken);

    private T GetService<T>(ref T? field) where T : ServiceBase, IDisposable
    {
        if (_serviceProvider == null)
        {
            return Roblox.Services.ServiceProvider.GetOrCreate<T>();
        }

        if (field != null)
        {
            return field;
        }

        field = _serviceProvider.GetRequiredService<T>();
        return field;
    }

    public void Dispose()
    {
        _leakCheck?.Dispose();
    }
}
