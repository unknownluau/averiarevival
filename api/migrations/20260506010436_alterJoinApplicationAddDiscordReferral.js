
exports.up = function(knex) {
  return knex.schema.alterTable('join_application', (table) => {
    table.string('discord_id', 512).nullable();
    table.string('discord_username', 32).nullable();
    table.integer('reffered_by').nullable();
  });
};

exports.down = function(knex) {
  return knex.schema.alterTable('join_application', (table) => {
    table.dropColumn('discord_id');
    table.dropColumn('discord_username');
    table.dropColumn('reffered_by');
  });
};
