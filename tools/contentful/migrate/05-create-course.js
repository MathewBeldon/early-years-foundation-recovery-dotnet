module.exports = function (migration) {
  const course = migration.createContentType("course", {
    name: "Course",
    displayField: "service_name",
    description: "Site-wide configuration for the .NET experiment",
  });

  course.createField("service_name", {
    name: "Service name",
    type: "Symbol",
    required: true,
  });

  course.createField("internal_mailbox", {
    name: "Internal mailbox",
    type: "Symbol",
    required: true,
  });

  course.createField("privacy_policy_url", {
    name: "Privacy policy",
    type: "Symbol",
    required: true,
  });

  course.createField("feedback", {
    name: "Feedback form",
    type: "Array",
    items: {
      type: "Link",
      linkType: "Entry",
      validations: [{ linkContentType: ["question"] }],
    },
  });
};
