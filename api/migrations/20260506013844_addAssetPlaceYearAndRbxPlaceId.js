
exports.up = function(knex) {
  return knex.schema.alterTable('asset_place', (table) => {
    table.bigInteger('year').notNullable().defaultTo(2017);
    table.bigInteger('roblox_place_id').notNullable().defaultTo(1818);
  });
};

exports.down = function(knex) {
  return knex.schema.alterTable('asset_place', (table) => {
    table.dropColumn('year');
    table.dropColumn('roblox_place_id');
  });
};
