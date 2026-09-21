const knexConfig = require("../knexfile");
const knex = require("knex")(knexConfig);

const CURRENT_USER_ID = 1;

const status = {
  open: 2,
  completed: 4,
  expired: 5,
  declined: 6,
  countered: 8,
};

const seededUsers = [
  "DonutsPlz",
  "TheAngryShift",
  "LegalSubstance",
  "Sole_Fern",
  "DaManSpoopy21",
  "Sayabienn",
  "Mooshoos",
  "Xmissile",
  "DeathmatchX18",
];

const assets = [
  { name: "Blue Wistful Wink", type: 18, rap: 1602, serial: 12821 },
  { name: "Blue Goof", type: 18, rap: 808, serial: 17134 },
  { name: "Chill Cap", type: 8, rap: 400, serial: null },
  { name: "Swordpack", type: 19, rap: 299, serial: null },
  { name: "Red Tango", type: 18, rap: 1220, serial: 9044 },
  { name: "Ruby Steel Fedora", type: 8, rap: 950, serial: 2020 },
  { name: "Poisoned Horns", type: 8, rap: 740, serial: 661 },
  { name: "Emerald Eye", type: 18, rap: 530, serial: 414 },
  { name: "Vinyl Backpack", type: 46, rap: 360, serial: null },
  { name: "Midnight Commando", type: 8, rap: 2200, serial: 77 },
  { name: "Neon Shades", type: 42, rap: 310, serial: null },
  { name: "Classic Sword", type: 19, rap: 180, serial: null },
  { name: "Egg Timer Hat", type: 8, rap: 250, serial: null },
  { name: "Silver Smile", type: 18, rap: 690, serial: 843 },
  { name: "Oldgen Visor", type: 8, rap: 145, serial: null },
];

const trades = [
  {
    partner: "DonutsPlz",
    direction: "inbound",
    state: status.open,
    createdDaysAgo: 2,
    expiresDaysFromNow: 28,
    myItems: ["Blue Wistful Wink"],
    partnerItems: ["Blue Goof", "Chill Cap", "Swordpack"],
  },
  {
    partner: "TheAngryShift",
    direction: "inbound",
    state: status.countered,
    createdDaysAgo: 3,
    expiresDaysFromNow: 26,
    myItems: ["Red Tango"],
    partnerItems: ["Ruby Steel Fedora", "Emerald Eye"],
    partnerRobux: 75,
  },
  {
    partner: "LegalSubstance",
    direction: "inbound",
    state: status.open,
    createdDaysAgo: 4,
    expiresDaysFromNow: 25,
    myItems: ["Vinyl Backpack", "Classic Sword"],
    partnerItems: ["Poisoned Horns"],
  },
  {
    partner: "Sole_Fern",
    direction: "outbound",
    state: status.open,
    createdDaysAgo: 5,
    expiresDaysFromNow: 24,
    myItems: ["Midnight Commando"],
    partnerItems: ["Silver Smile", "Oldgen Visor"],
  },
  {
    partner: "DaManSpoopy21",
    direction: "completed",
    state: status.completed,
    createdDaysAgo: 8,
    expiresDaysFromNow: -1,
    myItems: ["Neon Shades"],
    partnerItems: ["Egg Timer Hat"],
    myRobux: 50,
  },
  {
    partner: "Sayabienn",
    direction: "inactive",
    state: status.expired,
    createdDaysAgo: 16,
    expiresDaysFromNow: -2,
    myItems: ["Classic Sword"],
    partnerItems: ["Chill Cap"],
  },
  {
    partner: "Mooshoos",
    direction: "inactive",
    state: status.declined,
    createdDaysAgo: 11,
    expiresDaysFromNow: 18,
    myItems: ["Oldgen Visor"],
    partnerItems: ["Neon Shades"],
  },
  {
    partner: "Xmissile",
    direction: "inbound",
    state: status.open,
    createdDaysAgo: 6,
    expiresDaysFromNow: 23,
    myItems: ["Emerald Eye"],
    partnerItems: ["Classic Sword", "Egg Timer Hat"],
  },
  {
    partner: "DeathmatchX18",
    direction: "inbound",
    state: status.open,
    createdDaysAgo: 7,
    expiresDaysFromNow: 22,
    myItems: ["Silver Smile"],
    partnerItems: ["Vinyl Backpack"],
    partnerRobux: 120,
  },
];

const daysFromNow = (days) => {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);
  return date;
};

const pickId = (row) => Number(row.id || row);

const buildSettings = (columns, userId) => {
  const settings = { user_id: userId };
  const values = {
    inventory_privacy: 6,
    theme: 2,
    gender: 2,
    birthday: new Date("2000-01-01T00:00:00Z"),
    trade_privacy: 6,
    trade_filter: 1,
    private_message_privacy: 6,
    join_privacy: 1,
    avatar_page_style: 1,
  };

  for (const [key, value] of Object.entries(values)) {
    if (columns[key]) {
      settings[key] = value;
    }
  }

  return settings;
};

const ensureSettings = async (trx, columns, userId) => {
  await trx("user_settings").insert(buildSettings(columns, userId)).onConflict("user_id").ignore();
};

const ensureUser = async (trx, username) => {
  await trx("user").insert({
    username,
    password: "dev-seeded-password",
    status: 1,
    created_at: knex.fn.now(),
    online_at: knex.fn.now(),
  }).onConflict("username").merge({
    status: 1,
    online_at: knex.fn.now(),
  });

  const user = await trx("user").select("id").where({ username }).first();

  await trx("user_economy").insert({
    user_id: user.id,
    balance_robux: 1000,
    balance_tickets: 0,
  }).onConflict("user_id").ignore();

  return Number(user.id);
};

const insertAsset = async (trx, asset) => {
  const [row] = await trx("asset").insert({
    name: asset.name,
    description: "Seeded dev trade item.",
    asset_type: asset.type,
    asset_genre: 0,
    creator_type: 1,
    creator_id: CURRENT_USER_ID,
    moderation_status: 1,
    is_for_sale: false,
    price_robux: asset.rap,
    is_limited: true,
    is_limited_unique: asset.serial !== null,
    serial_count: asset.serial !== null ? 50000 : null,
    recent_average_price: asset.rap,
  }).returning("id");

  return pickId(row);
};

const insertUserAsset = async (trx, ownerId, assetId, serial) => {
  const [row] = await trx("user_asset").insert({
    user_id: ownerId,
    asset_id: assetId,
    serial,
    price: 0,
    created_at: knex.fn.now(),
    updated_at: knex.fn.now(),
  }).returning("id");

  return pickId(row);
};

const main = async () => {
  await knex.transaction(async (trx) => {
    const userSettingsColumns = await trx("user_settings").columnInfo();

    await trx("user").insert({
      id: CURRENT_USER_ID,
      username: "ROBLOX",
      password: "dev-seeded-password",
      status: 1,
      created_at: knex.fn.now(),
      online_at: knex.fn.now(),
    }).onConflict("id").ignore();

    await trx.raw("SELECT setval(pg_get_serial_sequence('\"user\"', 'id'), GREATEST((SELECT MAX(id) FROM \"user\"), 1))");

    const partnerIds = {};
    for (const username of seededUsers) {
      partnerIds[username] = await ensureUser(trx, username);
      await ensureSettings(trx, userSettingsColumns, partnerIds[username]);
    }

    await ensureSettings(trx, userSettingsColumns, CURRENT_USER_ID);

    await trx("user_economy").insert({
      user_id: CURRENT_USER_ID,
      balance_robux: 1000,
      balance_tickets: 0,
    }).onConflict("user_id").ignore();

    const existingTradeIds = await trx("user_trade")
      .select("id")
      .whereIn("user_id_one", [CURRENT_USER_ID, ...Object.values(partnerIds)])
      .whereIn("user_id_two", [CURRENT_USER_ID, ...Object.values(partnerIds)]);
    const tradeIds = existingTradeIds.map((row) => row.id);

    if (tradeIds.length > 0) {
      await trx("user_trade_asset").whereIn("trade_id", tradeIds).delete();
      await trx("user_trade").whereIn("id", tradeIds).delete();
    }

    const assetNames = assets.map((asset) => asset.name);
    const oldAssets = await trx("asset").select("id").whereIn("name", assetNames);
    const oldAssetIds = oldAssets.map((row) => row.id);

    if (oldAssetIds.length > 0) {
      await trx("asset_thumbnail").whereIn("asset_id", oldAssetIds).delete();
      await trx("asset_version").whereIn("asset_id", oldAssetIds).delete();
      await trx("user_asset").whereIn("asset_id", oldAssetIds).delete();
      await trx("asset").whereIn("id", oldAssetIds).delete();
    }

    const assetIds = {};
    for (const asset of assets) {
      assetIds[asset.name] = await insertAsset(trx, asset);
    }

    const userAssets = {};
    const grant = async (ownerId, assetName) => {
      const asset = assets.find((entry) => entry.name === assetName);
      const key = `${ownerId}:${assetName}`;
      if (!userAssets[key]) {
        userAssets[key] = await insertUserAsset(trx, ownerId, assetIds[assetName], asset.serial);
      }
      return userAssets[key];
    };

    for (const trade of trades) {
      const partnerId = partnerIds[trade.partner];
      const createdAt = daysFromNow(-trade.createdDaysAgo);
      const expiresAt = daysFromNow(trade.expiresDaysFromNow);
      const userOneId = trade.direction === "outbound" ? CURRENT_USER_ID : partnerId;
      const userTwoId = trade.direction === "outbound" ? partnerId : CURRENT_USER_ID;
      const userOneRobux = userOneId === CURRENT_USER_ID ? trade.myRobux || 0 : trade.partnerRobux || 0;
      const userTwoRobux = userTwoId === CURRENT_USER_ID ? trade.myRobux || 0 : trade.partnerRobux || 0;

      const [row] = await trx("user_trade").insert({
        user_id_one: userOneId,
        user_id_two: userTwoId,
        user_id_one_robux: userOneRobux,
        user_id_two_robux: userTwoRobux,
        status: trade.state,
        created_at: createdAt,
        updated_at: createdAt,
        expires_at: expiresAt,
      }).returning("id");

      const tradeId = pickId(row);
      const entries = [];

      for (const assetName of trade.myItems) {
        entries.push({ trade_id: tradeId, user_asset_id: await grant(CURRENT_USER_ID, assetName), user_id: CURRENT_USER_ID });
      }

      for (const assetName of trade.partnerItems) {
        entries.push({ trade_id: tradeId, user_asset_id: await grant(partnerId, assetName), user_id: partnerId });
      }

      await trx("user_trade_asset").insert(entries);
    }
  });

  console.log(`Seeded ${trades.length} dev trades for user ${CURRENT_USER_ID}.`);
};

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
}).finally(() => knex.destroy());
