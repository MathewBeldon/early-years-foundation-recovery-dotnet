module.exports = function (migration) {
  const question = migration.createContentType("question", {
    name: "Question",
    displayField: "name",
    description: "Course feedback questions for the .NET experiment",
  });

  question.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
  });

  question.createField("page_type", {
    name: "Page type",
    type: "Symbol",
    required: true,
    validations: [{ in: ["feedback", "thank_you"] }],
  });

  question.createField("input_type", {
    name: "Input type",
    type: "Symbol",
    required: true,
    validations: [{ in: ["radio", "checkbox", "textarea", "thank_you"] }],
  });

  question.createField("heading", { name: "Heading", type: "Text", required: true });
  question.createField("legend", { name: "Legend", type: "Text", required: true });
  question.createField("body", { name: "Body", type: "Text", required: true });

  question.createField("options", {
    name: "Options",
    type: "Array",
    items: { type: "Symbol" },
  });

  question.createField("skippable", { name: "Skippable", type: "Boolean" });
  question.createField("other", { name: "Other label", type: "Text" });
  question.createField("or", { name: "Or label", type: "Text" });
  question.createField("more", { name: "Allow more input", type: "Boolean" });
  question.createField("multi_select", { name: "Multi select", type: "Boolean" });
};
