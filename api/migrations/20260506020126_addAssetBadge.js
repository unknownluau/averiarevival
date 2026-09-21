
exports.up = function(knex) {
    return knex.schema.createTable('asset_badge', (table) => {
    table.bigInteger('asset_id').nullable();
    table.bigInteger('universe_id').nullable();
    table.boolean('enabled').notNullable().defaultTo(true);
  });
};

exports.down = function(knex) {
    return knex.schema.dropTable('asset_badge');
};
