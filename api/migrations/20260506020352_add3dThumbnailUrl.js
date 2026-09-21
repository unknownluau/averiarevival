
exports.up = function(knex) {
   return knex.schema.alterTable('user_avatar', (table) => {
    table.string('thumbnail_3d_url', 255).nullable().defaultTo(null);
  });
};

exports.down = function(knex) {
  return knex.schema.alterTable('user_avatar', (table) => {
    table.dropColumn('thumbnail_3d_url');
  });
};
