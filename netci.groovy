// Groovy Script: http://www.groovy-lang.org/syntax.html
// Jenkins DSL: https://github.com/jenkinsci/job-dsl-plugin/wiki

import jobs.generation.Utilities;

static getJobName(def opsysName, def configName, def testName) {
  return "${opsysName}_${configName}_${testName}"
}

static addArchival(def job, def filesToArchive, def filesToExclude) {
  def doNotFailIfNothingArchived = false
  def archiveOnlyIfSuccessful = false

  Utilities.addArchival(job, filesToArchive, filesToExclude, doNotFailIfNothingArchived, archiveOnlyIfSuccessful)
}

static addGithubPRTriggerForBranch(def job, def branchName, def jobName, def testName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"
  def triggerPhrase = "(?i).*test\\W+${prContext}.*"
  def triggerOnPhraseOnly = (testName != 'build')

  Utilities.addGithubPRTriggerForBranch(job, branchName, prContext, triggerPhrase, triggerOnPhraseOnly)
}

static addGithubPRCommitStatusForBranch(def job, def branchName, def jobName, def testName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"

  job.with {
    wrappers {
      downstreamCommitStatus {
        context(prContext)
      }
    }
  }
}

static addXUnitDotNETResults(def job, def configName) {
  def resultFilePattern = "**/artifacts/${configName}/log/xUnit-*.xml"
  def skipIfNoTestFiles = false

  Utilities.addXUnitDotNETResults(job, resultFilePattern, skipIfNoTestFiles)
}

static addExtendedEmailPublisher(def job) {
  job.with {
    publishers {
      extendedEmail {
        defaultContent('$DEFAULT_CONTENT')
        defaultSubject('$DEFAULT_SUBJECT')
        recipientList('$DEFAULT_RECIPIENTS, cc:tomat@microsoft.com')
        triggers {
          aborted {
            content('$PROJECT_DEFAULT_CONTENT')
            sendTo {
              culprits()
              developers()
              recipientList()
              requester()
            }
            subject('$PROJECT_DEFAULT_SUBJECT')
          }
          failure {
            content('$PROJECT_DEFAULT_CONTENT')
            sendTo {
              culprits()
              developers()
              recipientList()
              requester()
            }
            subject('$PROJECT_DEFAULT_SUBJECT')
          }
        }
      }
    }
  }
}

static addBuildSteps(def job, def projectName, def opsysName, def configName, def isPR) {
  def unit32JobName = getJobName(opsysName, configName, 'unit32')
  def unit32FullJobName = Utilities.getFullJobName(projectName, unit32JobName, isPR)

  def unit64JobName = getJobName(opsysName, configName, 'unit64')
  def unit64FullJobName = Utilities.getFullJobName(projectName, unit64JobName, isPR)

  def coreJobName = getJobName(opsysName, configName, 'core')
  def coreFullJobName = Utilities.getFullJobName(projectName, coreJobName, isPR)

  def downstreamFullJobNames = "${unit32FullJobName}, ${unit64FullJobName}, ${coreFullJobName}"

  def officialSwitch = ''

  if (!isPR) {
    officialSwitch = '-Official'
  }

  job.with {
    steps {
      batchFile("""set TEMP=%WORKSPACE%\\artifacts\\${configName}\\tmp
mkdir %TEMP%
set TMP=%TEMP%
.\\Build.cmd -Configuration ${configName} -msbuildVersion '14.0' ${officialSwitch} -SkipDeploy -SkipTest
""")
      publishers {
        downstreamParameterized {
          trigger(downstreamFullJobNames) {
            condition('UNSTABLE_OR_BETTER')
            parameters {
              currentBuild()
              gitRevision()
            }
          }
        }
      }
    }
  }
}

static addTestSteps(def job, def projectName, def opsysName, def configName, def testName, def isPR, def filesToArchive, def filesToExclude) {
  def buildJobName = getJobName(opsysName, configName, 'build')
  def buildFullJobName = Utilities.getFullJobName(projectName, buildJobName, isPR)

  def officialSwitch = ''

  if (!isPR) {
    officialSwitch = '-Official'
  }

  job.with {
    steps {
      copyArtifacts(buildFullJobName) {
        includePatterns(filesToArchive)
        excludePatterns(filesToExclude)
        buildSelector {
          upstreamBuild {
            fallbackToLastSuccessful(false)
          }
        }
      }
      batchFile("""set TEMP=%WORKSPACE%\\artifacts\\${configName}\\tmp
mkdir %TEMP%
set TMP=%TEMP%
.\\Build.cmd -Configuration ${configName} -msbuildVersion '14.0' ${officialSwitch} -SkipBuild ${testName == 'unit32' ? '-SkipTest64 -SkipTestCore' : (testName == 'core' ? '-SkipTest32 -SkipTest64' : '-SkipTest32 -SkipTestCore')}
""")
    }
  }
}

[true, false].each { isPR ->
  ['windows'].each { opsysName ->
    ['debug', 'release'].each { configName ->
        ['build', 'unit32', 'unit64', 'core'].each { testName ->
        def projectName = GithubProject

        def branchName = GithubBranchName

        def filesToArchive = "**/artifacts/${configName}/**"
        def filesToExclude = "**/artifacts/${configName}/obj/**"

        def jobName = getJobName(opsysName, configName, testName)
        def fullJobName = Utilities.getFullJobName(projectName, jobName, isPR)
        def myJob = job(fullJobName)

        Utilities.standardJobSetup(myJob, projectName, isPR, "*/${branchName}")

        if (testName == 'build') {
          if (isPR) {
            addGithubPRTriggerForBranch(myJob, branchName, jobName, testName)
          } else {
            Utilities.addGithubPushTrigger(myJob)
          }
        }
        else {
          if (isPR) {
            addGithubPRCommitStatusForBranch(myJob, branchName, jobName, testName)
          }
        }

        addArchival(myJob, filesToArchive, filesToExclude)

        if (testName != 'build') {
          addXUnitDotNETResults(myJob, configName)
        }

        Utilities.setMachineAffinity(myJob, 'Windows_NT', 'latest-or-auto-update3')

        if (!isPR) {
          addExtendedEmailPublisher(myJob)
        }

        if (testName == 'build') {
          addBuildSteps(myJob, projectName, opsysName, configName, isPR)
        } else {
          addTestSteps(myJob, projectName, opsysName, configName, testName, isPR, filesToArchive, filesToExclude)
        }
      }
    }
  }
}