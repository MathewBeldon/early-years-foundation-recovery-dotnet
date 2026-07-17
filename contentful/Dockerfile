FROM ruby:3.4.5-alpine3.22

RUN apk add --no-cache nodejs npm

WORKDIR /workspace/contentful

COPY contentful/package.json contentful/package-lock.json ./
RUN npm ci --ignore-scripts --no-audit

COPY contentful/ ./
COPY spec/support/ast/alpha-pass-response-with-feedback.yml /workspace/spec/support/ast/alpha-pass-response-with-feedback.yml

CMD ["npm", "run", "check"]
