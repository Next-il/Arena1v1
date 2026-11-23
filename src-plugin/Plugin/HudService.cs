using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;

using K4Arenas.Models;
using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;

using PanoramaManager;

namespace K4Arenas;

/// <summary>
/// The Arena 1v1 HUD: the round banner that replaces the round start chat line ("You are in arena 3 against shadow
/// and playing Rifle round"), the center HTML, and the two command tips in chat. The layout is
/// hud/panorama/layout/custom_game/arenas_hud.xml, shipped in the workshop addon.
///
/// <para><b>One layout, opened once per player and left open.</b> Its root is an invisible full-screen panel; the
/// banner inside carries its own "show". So showing it is a class toggle, never an Open/Close.</para>
///
/// <para><b>Per viewer.</b> Every player has their own arena and opponent, so every text and class is written with
/// SetVariableFor / SetClassFor.</para>
///
/// <para><b>Everything is redrawn from state.</b> The library rebuilds a player's panel by itself after a respawn,
/// a round restart or a map change, and drops every per-viewer variable and class on the way
/// (PanelAction.Restored). Each viewer's banner is therefore kept here and <see cref="Redraw"/> writes it back. A
/// banner whose time is up stays hidden.</para>
///
/// <para>Off when Hud.Enabled is false in the config: every caller then keeps its old chat and center lines.</para>
/// </summary>
public sealed class HudService : IDisposable
{
	private const string Layout = "panorama/layout/custom_game/arenas_hud.vxml_c";
	private const string CardId = "ar_rb";

	/// <summary>
	/// The arena is set at round prestart, before the round restart that respawns everyone. The banner comes in a
	/// moment later, once the player is in the arena. It is also long enough for a banner that is still up (the
	/// warmup refills arenas every 2 seconds) to go out and come back in: a class turned off and on again sooner
	/// than about 0.3 s does not reach the client.
	/// </summary>
	private const float ShowDelay = 0.35f;
	private const float ShowSeconds = 4.0f;

	/// <summary>The line under the title is text, command, text, command, text: five labels.</summary>
	private const int Pieces = 5;

	public readonly record struct Piece(string Text, bool Gap);

	/// <param name="Weapon">CS2 equipment icon name (ak47, awp, knife...), or "none".</param>
	/// <param name="SideValue">The arena number; empty hides the ARENA block.</param>
	public sealed record Banner(string Weapon, string Eyebrow, string Title, string Chip, string SideKey, string SideValue, IReadOnlyList<Piece> Sub);

	private sealed class Viewer
	{
		public Banner? Card;
		public float ShowAt;
		public float Until;

		/// <summary>Bumped for every banner, so the timers of an older one do nothing.</summary>
		public int Seq;

		/// <summary>Bumped every time the player is put in an arena.</summary>
		public int Round;
		public int CardRound = -1;

		/// <summary>The weapon handed out this round. A random one is only known once it is given.</summary>
		public string? Given;

		/// <summary>The w- class that is on for this viewer, so a new weapon turns one class off instead of 42.</summary>
		public string? AppliedWeapon;
	}

	/// <summary>
	/// Special rounds from Next-il/Arena1v1-Special-Rounds, by the name they register with. They hand out their own
	/// weapons, so the icon comes from here. Each title can be overridden in lang/ as k4.hud.round.&lt;name&gt;.
	/// </summary>
	internal sealed record Special(string Weapon, string Title, bool Headshots);

	internal static readonly Dictionary<string, Special> SpecialRounds = new(StringComparer.OrdinalIgnoreCase)
	{
		["OnlyHS-AK47"] = new("ak47", "AK-47 round", true),
		["OnlyHS-SSG08"] = new("ssg08", "Scout round", true),
		["OnlyHS-USP"] = new("usp_silencer", "USP round", true),
		["OneTap"] = new("ak47", "One tap round", false),
		["Nades"] = new("hegrenade", "Nade round", false),
		["NoCrosshair"] = new("none", "No crosshair round", false),
	};

	/// <summary>Every w- class arenas_hud.css defines.</summary>
	private static readonly HashSet<string> Icons =
	[
		"ak47", "aug", "awp", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar", "glock",
		"hkp2000", "m249", "m4a1", "m4a1_silencer", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev", "nova", "p250",
		"p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer", "xm1014",
		"knife", "taser", "hegrenade", "flashbang", "smokegrenade", "molotov", "incgrenade", "decoy",
	];

	private static readonly Dictionary<CsItem, string> ItemNames = BuildItemNames();

	private static readonly Regex Placeholder = new(@"\{([01])\}", RegexOptions.Compiled);

	private readonly Plugin _plugin;
	private readonly Dictionary<int, Viewer> _viewers = [];
	private PanelHandle? _panel;

	public HudService(Plugin plugin)
	{
		_plugin = plugin;
	}

	public bool Enabled => _panel != null;

	/// <summary>Spawns the layout once. A second Spawn would orphan the first entity, so this is guarded.</summary>
	public void Init()
	{
		if (_panel != null)
			return;

		try
		{
			Panorama.Init(_plugin);
			_panel = Panorama.Spawn(Layout, new LayoutContract
			{
				// Unique server-wide: dialog variables are keyed by panel id
				RootPanelId = "ArenasRoot",
				RevealClass = "show",
				RowCount = 0,
				CaptureInput = false,
				HideHud = HideHudFlags.None,
				SharedText = false,
			});
			_panel.OnEvent += OnPanelEvent;
			_plugin.Logger.LogInformation("[Hud] Arena round banner ready");
		}
		catch (Exception ex)
		{
			// The plugin works without its HUD: every caller falls back to chat when Enabled is false
			_plugin.Logger.LogWarning("[Hud] Arena round banner failed to start, using chat instead: {Message}", ex.Message);
			_panel = null;
		}
	}

	public void Dispose()
	{
		_panel?.Dispose();
		_panel = null;
		_viewers.Clear();
	}

	private void OnPanelEvent(PanelEvent e)
	{
		if (e.Action == PanelAction.Restored && e.Player.IsValid)
			Redraw(e.Player);
	}

	// ------------------------------------------------------------------ viewers

	private static bool IsHuman(CCSPlayerController? player) =>
		player is { IsValid: true, IsBot: false, IsHLTV: false } && player.Connected == PlayerConnectedState.Connected;

	private Viewer ViewerOf(CCSPlayerController player)
	{
		if (!_viewers.TryGetValue(player.Slot, out var viewer))
		{
			viewer = new Viewer();
			_viewers[player.Slot] = viewer;
		}

		return viewer;
	}

	/// <summary>Opens the player's session if it is not open yet, which also draws what they should see.</summary>
	private bool EnsureOpen(CCSPlayerController player)
	{
		if (_panel == null || !IsHuman(player))
			return false;

		if (!_panel.IsOpenFor(player))
		{
			_panel.Open(player);
			Redraw(player);
		}

		return _panel.IsOpenFor(player);
	}

	public void OnPlayerDisconnect(int slot)
	{
		_viewers.Remove(slot);
		_panel?.Close(slot);
	}

	/// <summary>
	/// Server.CurrentTime starts again on every map, so a banner from the last map could read as still up. The
	/// viewers stay: their AppliedWeapon still says which class is on.
	/// </summary>
	public void OnMapEnd()
	{
		foreach (var viewer in _viewers.Values)
		{
			viewer.Card = null;
			viewer.Given = null;
			viewer.ShowAt = 0;
			viewer.Until = 0;
		}
	}

	/// <summary>Writes the banner back for one player, from state. Called on open and after the library rebuilt the panel.</summary>
	public void Redraw(CCSPlayerController player)
	{
		if (_panel == null || !_panel.IsOpenFor(player))
			return;

		var viewer = ViewerOf(player);
		var now = Server.CurrentTime;

		Apply(player, viewer, viewer.Card != null && now >= viewer.ShowAt && now < viewer.Until, forceWeapon: true);
	}

	private CCSPlayerController? Current(int slot, int seq)
	{
		var player = Utilities.GetPlayerFromSlot(slot);

		return IsHuman(player) && _viewers.TryGetValue(slot, out var viewer) && viewer.Seq == seq && _panel?.IsOpenFor(player!) == true
			? player
			: null;
	}

	// ------------------------------------------------------------------ round start

	/// <summary>A player was put in an arena. Called before they spawn, so a weapon given from here on is this round's.</summary>
	public void BeginRound(CCSPlayerController player)
	{
		if (_panel == null || !IsHuman(player))
			return;

		var viewer = ViewerOf(player);
		viewer.Round++;
		viewer.Given = null;
	}

	/// <summary>
	/// The weapon the player was just handed (ArenaPlayer.SetupWeapons). It decides the icon when the round's weapon
	/// is random, and corrects a guess.
	/// </summary>
	public void WeaponGiven(CCSPlayerController player, CsItem item)
	{
		if (_panel == null || !IsHuman(player))
			return;

		var viewer = ViewerOf(player);
		var name = IconOf(item);
		viewer.Given = name;

		if (viewer.Card != null && viewer.CardRound == viewer.Round && viewer.Card.Weapon != name)
		{
			viewer.Card = viewer.Card with { Weapon = name };

			if (_panel.IsOpenFor(player))
				ApplyWeapon(player, viewer, name, force: false);
		}
	}

	/// <summary>
	/// Shows one player their arena for this round: the weapon, their opponent, the round type, the arena number, and
	/// the command tips (or "Top arena. Win to stay." in arena 1). Replaces the round start chat line and the center
	/// HTML for that player.
	/// </summary>
	public void ShowArena(ArenaPlayer player, int arenaId, RoundType round, List<ArenaPlayer>? opponents)
	{
		var controller = player.Controller;

		if (_panel == null || !IsHuman(controller))
			return;

		var card = BuildBanner(player, arenaId, round, opponents);
		var viewer = ViewerOf(controller);

		if (card.Weapon == "none" && viewer.Given != null)
			card = card with { Weapon = viewer.Given };

		var now = Server.CurrentTime;
		viewer.Card = card;
		viewer.CardRound = viewer.Round;
		viewer.ShowAt = now + ShowDelay;
		viewer.Until = viewer.ShowAt + ShowSeconds;
		var seq = ++viewer.Seq;

		if (!EnsureOpen(controller))
			return;

		// The text now, hidden: a banner still up from the last arena goes out first, then this one comes in
		Apply(controller, viewer, false, forceWeapon: false);

		var slot = controller.Slot;

		_plugin.AddTimer(ShowDelay, () =>
		{
			if (Current(slot, seq) is { } again)
				Apply(again, _viewers[slot], true, forceWeapon: false);
		}, TimerFlags.STOP_ON_MAPCHANGE);

		_plugin.AddTimer(ShowDelay + ShowSeconds, () =>
		{
			if (Current(slot, seq) is { } again)
				Cls(again, CardId, "show", false);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	private Banner BuildBanner(ArenaPlayer player, int arenaId, RoundType round, List<ArenaPlayer>? opponents)
	{
		var controller = player.Controller;
		var hasOpponent = opponents?.Any(p => p.IsValid) == true;
		var names = hasOpponent ? _plugin.GetOpponentNames(controller, opponents) : string.Empty;

		var eyebrow = !hasOpponent
			? Text(controller, "k4.general.no_opponent", "No opponent")
			: arenaId switch
			{
				-2 => Text(controller, "k4.hud.vs_challenge", "Challenge vs {0}", names),
				-1 => Text(controller, "k4.hud.vs_warmup", "Warmup vs {0}", names),
				_ => Text(controller, "k4.hud.vs", "vs {0}", names),
			};

		string title;
		var chip = string.Empty;

		if (arenaId == -1)
		{
			// The warmup hands out a random weapon, like the old line's "playing Random round"
			title = Text(controller, "k4.hud.round", "{0} round", Lang(controller, "k4.general.random", "Random"));
		}
		else if (round.StartFunction != null)
		{
			var name = round.Name ?? string.Empty;
			var special = SpecialRounds.GetValueOrDefault(name);
			title = Text(controller, $"k4.hud.round.{name}", special?.Title ?? Text(controller, "k4.hud.round", "{0} round", name));

			if (special?.Headshots == true)
				chip = Text(controller, "k4.hud.chip.hs", "HS");
		}
		else
		{
			var name = round.Name ?? string.Empty;
			title = Text(controller, "k4.hud.round", "{0} round", Lang(controller, name, name));
		}

		var numbered = arenaId > 0;
		var sideKey = numbered ? Lang(controller, "k4.general.arena", "ARENA") : string.Empty;
		var sideValue = numbered ? arenaId.ToString() : string.Empty;

		var sub = arenaId == 1
			? [new Piece(Text(controller, "k4.hud.top_arena", "Top arena. Win to stay."), false)]
			: Tips(controller);

		return new Banner(PredictWeapon(player, round), eyebrow, title, chip, sideKey, sideValue, sub);
	}

	/// <summary>
	/// "Pick round types with !rounds. Sit out with !afk.", with the commands taken from the config: the first of
	/// round-pref-commands (registered only with a database) and the first of afk-commands.
	/// </summary>
	private IReadOnlyList<Piece> Tips(CCSPlayerController player)
	{
		var commands = _plugin.Config.CommandSettings;
		var rounds = _plugin.HasDatabase ? commands.RoundsCommands.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) : null;
		var afk = commands.AFKCommands.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

		if (rounds != null && afk != null)
			return Split(Lang(player, "k4.hud.tips", "Pick round types with {0}. Sit out with {1}."), $"!{rounds}", $"!{afk}");

		if (rounds != null)
			return Split(Lang(player, "k4.hud.tips_rounds", "Pick round types with {0}."), $"!{rounds}");

		if (afk != null)
			return Split(Lang(player, "k4.hud.tips_afk", "Sit out with {0}."), $"!{afk}");

		return [];
	}

	/// <summary>
	/// Cuts a phrase at {0} and {1} into text, command, text, command, text. A Label trims its own edges, so where
	/// the phrase had a space at a cut the next piece gets "gap" instead.
	/// </summary>
	private static List<Piece> Split(string format, params string[] commands)
	{
		var pieces = new List<Piece>();
		var pos = 0;
		var shown = false;

		void Add(string raw, bool spaceBefore, bool isCommand)
		{
			var text = Clean(isCommand ? raw : raw.Trim());
			pieces.Add(new Piece(text, shown && text.Length > 0 && spaceBefore));
			shown |= text.Length > 0;
		}

		foreach (Match match in Placeholder.Matches(format))
		{
			if (pieces.Count >= Pieces - 1)
				break;

			var raw = format[pos..match.Index];
			Add(raw, raw.Length > 0 && char.IsWhiteSpace(raw[0]), false);

			var index = match.Groups[1].Value[0] - '0';
			var command = index < commands.Length ? commands[index] : string.Empty;
			Add(command, raw.Length > 0 && char.IsWhiteSpace(raw[^1]), true);

			pos = match.Index + match.Length;
		}

		var rest = format[pos..];
		Add(rest, rest.Length > 0 && char.IsWhiteSpace(rest[0]), false);

		return pieces;
	}

	/// <summary>
	/// The icon for the weapon this player gets, as far as it is known before they spawn. A random pick is not known
	/// yet: "none" until <see cref="WeaponGiven"/> says what it was. Mirrors ArenaPlayer.SetupWeapons.
	/// </summary>
	private string PredictWeapon(ArenaPlayer player, RoundType round)
	{
		if (round.StartFunction != null)
			return SpecialRounds.GetValueOrDefault(round.Name ?? string.Empty)?.Weapon ?? "none";

		if (round.PrimaryPreference == WeaponType.Unknown)
			return "none";

		if (round.PrimaryWeapon is { } primary)
			return IconOf(primary);

		if (round.UsePreferredPrimary && round.PrimaryPreference is { } type)
			return player.WeaponPreferences.GetValueOrDefault(type) is { } preferred ? IconOf(preferred) : "none";

		if (round.SecondaryWeapon is { } secondary)
			return IconOf(secondary);

		if (round.UsePreferredSecondary)
			return player.WeaponPreferences.GetValueOrDefault(WeaponType.Pistol) is { } pistol ? IconOf(pistol) : "none";

		return _plugin.Config.CompatibilitySettings.GiveKnifeByDefault ? "knife" : "none";
	}

	// ------------------------------------------------------------------ drawing

	private void Set(CCSPlayerController player, string name, string value) => _panel!.SetVariableFor(player, name, value);

	private void Cls(CCSPlayerController player, string id, string cls, bool on) => _panel!.SetClassFor(player, id, cls, on);

	private void Apply(CCSPlayerController player, Viewer viewer, bool show, bool forceWeapon)
	{
		if (_panel == null || !_panel.IsOpenFor(player))
			return;

		var card = viewer.Card;

		if (card != null)
		{
			Set(player, "ar_rb_eyebrow", card.Eyebrow);
			Set(player, "ar_rb_title", card.Title);
			Set(player, "ar_rb_chip", card.Chip);
			Set(player, "ar_rb_side_k", card.SideKey);
			Set(player, "ar_rb_side_v", card.SideValue);

			var anySub = false;

			for (var i = 0; i < Pieces; i++)
			{
				var piece = i < card.Sub.Count ? card.Sub[i] : default;
				var text = piece.Text ?? string.Empty;
				anySub |= text.Length > 0;

				Set(player, $"ar_rb_s{i}", text);
				Cls(player, $"ar_rb_s{i}", "hidden", text.Length == 0);
				Cls(player, $"ar_rb_s{i}", "gap", piece.Gap);
			}

			Cls(player, CardId, "has-chip", card.Chip.Length > 0);
			Cls(player, CardId, "no-side", card.SideValue.Length == 0);
			Cls(player, CardId, "no-sub", !anySub);
			ApplyWeapon(player, viewer, card.Weapon, forceWeapon);
		}

		Cls(player, CardId, "show", show && card != null);
	}

	/// <summary>Exactly one w- class on. Forced after the panel was rebuilt, when the record may be out of date.</summary>
	private void ApplyWeapon(CCSPlayerController player, Viewer viewer, string weapon, bool force)
	{
		if (!force && viewer.AppliedWeapon == weapon)
			return;

		if (viewer.AppliedWeapon != null && viewer.AppliedWeapon != weapon)
			Cls(player, CardId, $"w-{viewer.AppliedWeapon}", false);

		Cls(player, CardId, $"w-{weapon}", true);
		viewer.AppliedWeapon = weapon;
	}

	// ------------------------------------------------------------------ weapons

	private static Dictionary<CsItem, string> BuildItemNames()
	{
		var names = new Dictionary<CsItem, string>();

		foreach (var field in typeof(CsItem).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			var value = field.GetCustomAttribute<EnumMemberAttribute>()?.Value;

			if (value != null && field.GetValue(null) is CsItem item)
				names.TryAdd(item, value);
		}

		return names;
	}

	/// <summary>weapon_ak47 -> ak47, any knife -> knife; "none" when the layout has no icon for it.</summary>
	public static string IconOf(CsItem item)
	{
		if (!ItemNames.TryGetValue(item, out var name))
			return "none";

		if (name.StartsWith("weapon_", StringComparison.Ordinal))
			name = name["weapon_".Length..];

		if (name.StartsWith("knife", StringComparison.Ordinal))
			name = "knife";

		return Icons.Contains(name) ? name : "none";
	}

	// ------------------------------------------------------------------ text

	/// <summary>A phrase from lang/ in the player's language, or the English fallback when the lang file does not have it.</summary>
	private string Lang(CCSPlayerController player, string key, string fallback)
	{
		var text = _plugin.Localizer.ForPlayer(player, key);

		return string.IsNullOrEmpty(text) || text == key ? fallback : text;
	}

	private string Text(CCSPlayerController player, string key, string fallback, params object[] args)
	{
		var format = Lang(player, key, fallback);

		try
		{
			return Clean(args.Length == 0 ? format : string.Format(format, args));
		}
		catch (FormatException)
		{
			return Clean(string.Format(fallback, args));
		}
	}

	/// <summary>Drops chat color codes and other control characters: a Label would draw them as boxes.</summary>
	private static string Clean(string text)
	{
		if (text.All(c => !char.IsControl(c)))
			return text;

		return new string(text.Where(c => !char.IsControl(c)).ToArray());
	}
}
