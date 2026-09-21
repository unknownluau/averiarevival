/**
 * Add various user tables
 * @param {import('knex')} knex
 */
exports.up = async (knex) => {
  await knex.schema.createTable('user_totp', (table) => {
    table.bigInteger('user_id');
    table.text('secret');
    table.integer('status').notNullable().defaultTo(2);
    table.primary(['user_id']);
  });
};

exports.down = async (knex) => {
  await knex.schema.dropTable('user_totp');
};