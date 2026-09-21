/**
 * @param { import("knex").Knex } knex
 * @returns { Promise<void> }
 */
exports.up = function(knex) {
  return knex.schema.createTable('user_mac_address', (table) => {
    table.bigInteger('user_id').unsigned().notNullable().references('id').inTable('user').onDelete('CASCADE');
    
    table.specificType('mac_address', 'macaddr').notNullable();
    
    table.timestamp('created_at', { useTz: true }).defaultTo(knex.fn.now());
    table.timestamp('updated_at', { useTz: true }).defaultTo(knex.fn.now());

    // Composite Primary Key (user_id + mac_address)
    // This ensures a user can't have duplicate MAC entries and speeds up lookups
    table.primary(['user_id', 'mac_address']);
  });
};

/**
 * @param { import("knex").Knex } knex
 * @returns { Promise<void> }
 */
exports.down = function(knex) {
  return knex.schema.dropTableIfExists('user_mac_address');
};
