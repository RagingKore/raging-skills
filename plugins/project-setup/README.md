# Project Setup

Generate or update `.project/` documentation structure for any codebase. Designed for initializing
new projects, onboarding, or refreshing stale project context.

## Overview

This plugin analyzes a codebase using parallel discovery agents and generates persistent project
documentation that follows a strict editorial philosophy: document only what breaks convention.
It produces a `.project/project.md` lobby file and optional topic files for areas that warrant
dedicated coverage.

## Skills

### User-Invoked

**Project Setup** (`/project-setup [force]`)

Dispatches three parallel agents (compliance, build, structure) to scan the codebase, synthesizes
findings, presents topics for user approval, and generates documentation from templates. Supports
two modes:

- **Fresh mode**: full codebase scan and generation from scratch
- **Review mode**: delta analysis against existing `.project/` files with targeted updates

Manages symlinks for `CLAUDE.md`, `AGENTS.md`, `GEMINI.md`, and other agent instruction files
pointing to the generated `project.md`.
