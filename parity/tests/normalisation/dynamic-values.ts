export interface DynamicValueMask {
  pattern: RegExp;
  replacement: string;
  justification: string;
}

const defaultMasks: ReadonlyArray<DynamicValueMask> = [
  {
    pattern: /nonce=["'][^"']+["']/gi,
    replacement: 'nonce="<nonce>"',
    justification: 'CSP nonces are intentionally unique for every response.',
  },
  {
    pattern: /(name=["']csp-nonce["']\s+content=["'])[^"']+(["'])/gi,
    replacement: '$1<nonce>$2',
    justification: 'The CSP nonce meta value is intentionally unique for every response.',
  },
  {
    pattern: /(name=["']csrf-token["']\s+content=["'])[^"']+(["'])/gi,
    replacement: '$1<csrf-token>$2',
    justification: 'Rails authenticity tokens are intentionally unique per session.',
  },
  {
    pattern: /(name=["']authenticity_token["'][^>]*\svalue=["'])[^"']+(["'])/gi,
    replacement: '$1<csrf-token>$2',
    justification: 'Rails masks authenticity tokens independently for each rendered form.',
  },
];

export function maskDynamicValues(
  value: string,
  approvedMasks: ReadonlyArray<DynamicValueMask> = [],
): string {
  for (const mask of [...defaultMasks, ...approvedMasks]) {
    if (!mask.justification.trim()) {
      throw new Error(`Dynamic value mask ${mask.pattern} has no justification.`);
    }
  }

  return [...defaultMasks, ...approvedMasks].reduce(
    (normalised, mask) => normalised.replace(mask.pattern, mask.replacement),
    value,
  );
}
