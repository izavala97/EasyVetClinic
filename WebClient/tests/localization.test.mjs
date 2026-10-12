import test from 'node:test'
import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import ts from 'typescript'
import i18n, { translate, languages, subscribeLanguage } from '../src/i18n.ts'
import { spanish } from '../src/locales/es.ts'
import { formatMoney, formatDateTime, formatDate } from '../src/format.ts'

test('English is the default and Spanish is supported without translating saved data', () => {
  assert.deepEqual(languages.map(item => item.code), ['en', 'es'])
  assert.equal(translate('Clinic profile', {}, 'en'), 'Clinic profile')
  assert.equal(translate('Clinic profile', {}, 'es'), 'Perfil de la clínica')
  assert.equal(translate('Luna Hernández', {}, 'es'), 'Luna Hernández')
})

test('clinic language changes notify all subscribers and reject unsupported languages', () => {
  i18n.changeLanguage('en')
  let notifications = 0
  const unsubscribe = subscribeLanguage(() => notifications++)
  i18n.changeLanguage('es')
  assert.equal(i18n.t('Save clinic'), 'Guardar clínica')
  assert.equal(notifications, 1)
  i18n.changeLanguage('es')
  assert.equal(notifications, 1)
  assert.throws(() => i18n.changeLanguage('fr'), /Unsupported clinic language/)
  assert.equal(i18n.language, 'es')
  i18n.changeLanguage('en')
  assert.equal(i18n.t('Save clinic'), 'Save clinic')
  unsubscribe()
})

test('interpolation and plurals work in both languages', () => {
  assert.equal(translate('Add guardian {{value0}}', { value0: 'María' }, 'es'), 'Añadir tutor María')
  assert.equal(translate('{{count}} matching contacts.', { count: 1 }, 'en'), '1 matching contact.')
  assert.equal(translate('{{count}} matching contacts.', { count: 2 }, 'en'), '2 matching contacts.')
  assert.equal(translate('{{count}} matching contacts.', { count: 1 }, 'es'), '1 tutor encontrado.')
  assert.equal(translate('{{count}} matching contacts.', { count: 2 }, 'es'), '2 tutores encontrados.')
  assert.throws(() => translate('Add guardian {{value0}}', {}, 'es'), /Missing translation variable/)
})

test('formatting follows clinic language without changing dates or currencies', () => {
  const date = new Date('2026-10-09T17:00:00Z')
  for (const language of ['en', 'es']) {
    i18n.changeLanguage(language)
    assert.equal(formatMoney(1234.5, 'MXN'), new Intl.NumberFormat(language, { style: 'currency', currency: 'MXN' }).format(1234.5))
    assert.equal(formatDateTime(date), new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeStyle: 'short' }).format(date))
    assert.equal(formatDate('2026-10-09'), new Intl.DateTimeFormat(language, { dateStyle: 'medium' }).format(new Date('2026-10-09T12:00:00')))
  }
  i18n.changeLanguage('en')
})

test('every literal translation key used by the UI has a Spanish resource', () => {
  const root = fileURLToPath(new URL('../src/', import.meta.url))
  const files = ['App.tsx', 'ClinicLanguage.tsx', 'printing.ts', ...['pages', 'components'].flatMap(dir =>
    readdirSync(path.join(root, dir)).filter(file => file.endsWith('.tsx')).map(file => path.join(dir, file)))]
  const missing = new Set()
  for (const file of files) {
    const text = readFileSync(path.join(root, file), 'utf8')
    const ast = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
    function visit(node) {
      if (ts.isCallExpression(node) && ['t', 'i18n.t'].includes(node.expression.getText(ast))) {
        const argument = node.arguments[0]
        if (argument && ts.isStringLiteral(argument) && !(argument.text in spanish) && !(`${argument.text}_other` in spanish)) {
          missing.add(`${file}: ${argument.text}`)
        }
      }
      ts.forEachChild(node, visit)
    }
    visit(ast)
  }
  assert.deepEqual([...missing], [])
})

test('translation placeholders match English keys and each other for plurals', () => {
  const variables = text => [...text.matchAll(/\{\{(\w+)\}\}/g)].map(match => match[1]).sort()
  for (const [key, value] of Object.entries(spanish)) {
    assert.deepEqual(variables(value), variables(key), key)
  }
})

test('translated form labels never become submitted values', () => {
  const root = fileURLToPath(new URL('../src/pages/', import.meta.url))
  const invalid = []
  for (const file of readdirSync(root).filter(file => file.endsWith('.tsx'))) {
    const ast = ts.createSourceFile(file, readFileSync(path.join(root, file), 'utf8'), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
    const containsTranslation = node => {
      if (ts.isCallExpression(node) && ['t', 'i18n.t'].includes(node.expression.getText(ast))) return true
      return ts.forEachChild(node, containsTranslation) ?? false
    }
    function visit(node) {
      if (ts.isJsxAttribute(node) && ['value', 'defaultValue'].includes(node.name.getText(ast)) &&
        node.initializer && containsTranslation(node.initializer)) invalid.push(`${file}: ${node.getText(ast)}`)
      if (ts.isJsxElement(node) && node.openingElement.tagName.getText(ast) === 'option' &&
        node.children.some(containsTranslation) &&
        !node.openingElement.attributes.properties.some(attribute => ts.isJsxAttribute(attribute) && attribute.name.getText(ast) === 'value')) {
        invalid.push(`${file}: translated option without a stable value`)
      }
      ts.forEachChild(node, visit)
    }
    visit(ast)
  }
  assert.deepEqual(invalid, [])
})
