module.exports = function (migration) {
  const staticPage = migration.createContentType("static", {
    name: "Static",
    displayField: "name",
    description: "Stand-alone pages for the .NET experiment",
  });

  staticPage.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  staticPage.createField("title", { name: "Title", type: "Symbol", required: true });
  staticPage.createField("heading", { name: "Heading", type: "Symbol", required: true });
  staticPage.createField("body", { name: "Body", type: "Text", required: true });
  staticPage.createField("footer", {
    name: "Add to footer",
    type: "Boolean",
    required: true,
    defaultValue: { "en-US": false },
  });
  staticPage.createField("requires_auth", {
    name: "Requires auth",
    type: "Boolean",
    required: true,
    defaultValue: { "en-US": false },
  });
};
