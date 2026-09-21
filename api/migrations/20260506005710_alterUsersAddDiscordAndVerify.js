
exports.up = function(knex) {
  return knex.schema.alterTable('user', (table) => {
    table.text('discord_id').nullable();
    table.text('discordAuthCode').nullable();
    table.integer('linkstatus').notNullable().defaultTo(3);
    table.boolean('verified').notNullable().defaultTo(false);
  });
};

exports.down = function(knex) {
  return knex.schema.alterTable('user', (table) => {
    table.dropColumn('discord_id');
    table.dropColumn('discordAuthCode');
    table.dropColumn('linkstatus');
    table.dropColumn('verified');
  });
};
