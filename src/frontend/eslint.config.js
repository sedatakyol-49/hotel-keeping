// @ts-check
const eslint = require('@eslint/js');
const { defineConfig, globalIgnores } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const prettier = require('eslint-config-prettier/flat');

module.exports = defineConfig([
  // OpenAPI'dan uretilen istemci kodu lint kapsami disindadir.
  globalIgnores(['dist/**', '.angular/**', 'coverage/**', 'src/app/core/api/generated/**']),
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
      prettier,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'hc',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'hc',
          style: 'kebab-case',
        },
      ],
      // "Otel Defteri" kurallari: standalone + OnPush zorunlu.
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-standalone': 'error',
      '@angular-eslint/use-lifecycle-interface': 'error',
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
  {
    /*
     * Misafir sitesi kendi secici on ekini kullanir: `hcg-*`. Panel bilesenleri
     * `hc-*` kalir, paylasilan katman da `hc-*` (iki uygulamada da ayni etiket).
     * Boylece bir sablona bakan kisi bilesenin hangi uygulamaya ait oldugunu
     * etiketten anlar; bu ayrim ozellikle testlerde ve DOM incelemesinde ise yarar.
     */
    files: ['projects/guest-web/**/*.ts'],
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'hcg', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'hcg', style: 'kebab-case' },
      ],
    },
  },
  {
    /*
     * `server.ts` bir Node giris noktasidir: konsola yazmasi ve `process`
     * kullanmasi beklenen davranistir.
     */
    files: ['projects/guest-web/src/server.ts'],
    rules: {
      'no-console': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility, prettier],
    rules: {},
  },
]);
