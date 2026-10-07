export type PrintableDocument = { title: string; fields: Record<string, string>; preparedBy: string }

export async function printDocument(load: () => Promise<PrintableDocument>) {
  const preview = window.open('', '_blank')
  if (!preview) throw new Error('Allow pop-ups to print the document.')
  preview.opener = null
  preview.document.title = 'Preparing document'
  preview.document.body.textContent = 'Preparing document...'
  try {
    const draft = await load()
    preview.document.title = draft.title
    const content = preview.document.createElement('pre')
    content.style.whiteSpace = 'pre-wrap'
    content.style.font = '16px system-ui'
    content.textContent = [draft.title, 'Draft — review before signing', '', ...Object.entries(draft.fields).map(([key, value]) => `${key}: ${value}`),
      '', `Prepared by ${draft.preparedBy}`].join('\n')
    preview.document.body.replaceChildren(content)
    preview.focus()
    preview.print()
  } catch (cause) {
    preview.close()
    throw cause
  }
}
