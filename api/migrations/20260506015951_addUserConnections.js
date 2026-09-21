
exports.up = function(knex) {
  return knex.schema.createTable('user_connections', (table) => {
    table.bigInteger('user_id').notNullable().primary();
    table.string('twitter', 15).nullable();
    table.string('youtube', 30).nullable();
    table.string('tiktok', 24).nullable();
    table.string('discord', 32).nullable();
    table.string('telegram', 32).nullable();
    table.string('twitch', 25).nullable();
    table.string('github', 39).nullable();
    table.string('roblox', 20).nullable();
  });
};

exports.down = function(knex) {
    return knex.schema.dropTable('user_connections');
};
