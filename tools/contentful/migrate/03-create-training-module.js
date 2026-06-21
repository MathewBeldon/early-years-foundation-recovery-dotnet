module.exports = function (migration) {
  const trainingModule = migration.createContentType("trainingModule", {
    name: "Training Module",
    displayField: "title",
    description: "Top-level training module for the .NET experiment",
  });

  trainingModule.createField("title", { name: "Title", type: "Symbol", required: true });
  trainingModule.createField("name", { name: "Name", type: "Symbol", required: true });
  trainingModule.createField("description", { name: "Description", type: "Text", required: true });
  trainingModule.createField("outcomes", { name: "Outcomes", type: "Text", required: true });
  trainingModule.createField("criteria", { name: "Criteria", type: "Text", required: true });
  trainingModule.createField("duration", { name: "Duration", type: "Number", required: true });
  trainingModule.createField("position", { name: "Position", type: "Integer", required: true });
  trainingModule.createField("live", { name: "Live", type: "Boolean", required: true });
  trainingModule.createField("upcoming", { name: "Upcoming", type: "Text" });

  trainingModule.createField("pages", {
    name: "Pages",
    type: "Array",
    items: {
      type: "Link",
      linkType: "Entry",
      validations: [{ linkContentType: ["page"] }],
    },
  });
};
