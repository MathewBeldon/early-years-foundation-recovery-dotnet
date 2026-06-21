module.exports = function (migration) {
  const page = migration.createContentType("page", {
    name: "Page",
    displayField: "name",
    description: "Textual and question pages for the .NET experiment",
  });

  page.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
  });

  page.createField("page_type", {
    name: "Page type",
    type: "Symbol",
    required: true,
    validations: [
      {
        in: [
          "text_page",
          "interruption_page",
          "topic_intro",
          "summary_intro",
          "submodule_intro",
          "assessment_intro",
          "assessment_results",
          "confidence_intro",
          "recap_page",
          "certificate",
          "formative",
          "summative",
        ],
      },
    ],
  });

  page.createField("heading", { name: "Heading", type: "Text", required: true });
  page.createField("body", { name: "Body", type: "Text", required: true });

  page.createField("notes", {
    name: "Notes",
    type: "Boolean",
    required: true,
    defaultValue: { "en-US": false },
  });

  page.createField("answers", {
    name: "Answers",
    type: "Object",
  });

  page.createField("success_message", {
    name: "Success message",
    type: "Text",
  });

  page.createField("failure_message", {
    name: "Failure message",
    type: "Text",
  });

  page.changeFieldControl("answers", "builtin", "objectEditor", {
    helpText: '[{"text":"Correct answer","correct":true},{"text":"Wrong answer","correct":false}]',
  });
};
