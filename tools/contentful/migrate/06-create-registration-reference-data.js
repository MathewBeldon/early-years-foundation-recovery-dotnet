module.exports = function (migration) {
  const userSetting = migration.createContentType("userSetting", {
    name: "User setting",
    displayField: "title",
    description: "Registration setting types, compatible with the original Rails userSetting model",
  });

  userSetting.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  userSetting.createField("title", {
    name: "Title",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  userSetting.createField("local_authority", {
    name: "Local authority",
    type: "Boolean",
    required: true,
    defaultValue: { "en-US": false },
  });

  userSetting.createField("role_type", {
    name: "Role type",
    type: "Symbol",
    required: true,
    validations: [{ in: ["childminder", "other", "none"] }],
  });

  userSetting.createField("active", {
    name: "Active",
    type: "Boolean",
    required: false,
    defaultValue: { "en-US": true },
  });

  const role = migration.createContentType("registrationRole", {
    name: "Registration role",
    displayField: "name",
    description: "Registration role options filtered by setting role_type group",
  });

  role.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  role.createField("group", {
    name: "Group",
    type: "Symbol",
    required: true,
    validations: [{ in: ["childminder", "other"] }],
  });

  role.createField("hint_text", {
    name: "Hint text",
    type: "Text",
    required: false,
  });

  const country = migration.createContentType("registrationCountry", {
    name: "Registration country",
    displayField: "name",
    description: "Where-you-live options for registration",
  });

  country.createField("id", {
    name: "ID",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  country.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
  });

  const localAuthority = migration.createContentType("registrationLocalAuthority", {
    name: "Registration local authority",
    displayField: "name",
    description: "Local authority options for registration",
  });

  localAuthority.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  const experience = migration.createContentType("registrationExperience", {
    name: "Registration experience",
    displayField: "name",
    description: "Early-years experience options for registration",
  });

  experience.createField("id", {
    name: "ID",
    type: "Symbol",
    required: true,
    validations: [{ unique: true }],
  });

  experience.createField("name", {
    name: "Name",
    type: "Symbol",
    required: true,
  });
};
