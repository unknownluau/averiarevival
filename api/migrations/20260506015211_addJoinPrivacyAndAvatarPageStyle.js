
exports.up = function(knex) {
  return knex.schema.alterTable('user_settings', (table) => {
    table.integer('join_privacy').notNullable().defaultTo(1);
    table.integer('avatar_page_style').notNullable().defaultTo(1);
  });
};

exports.down = function(knex) {
  return knex.schema.alterTable('user_settings', (table) => {
    table.dropColumn('join_privacy');
    table.dropColumn('avatar_page_style');
  });
};
